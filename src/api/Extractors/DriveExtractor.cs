using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Scans OneDrive/SharePoint Office documents for commitment signals via two paths:
///
///   Path A — Inline comments (Word/Excel/PowerPoint Review comments):
///     Only for files where the current user is the creator or last editor.
///     Downloads the Office ZIP archive and parses the XML/JSON comment stores:
///       DOCX: word/comments.xml        → <w:comment> elements
///       XLSX: xl/comments{N}.xml       → <comment> elements
///       PPTX: ppt/comments/*.json      → modern comment JSON (Office 365)
///             ppt/comments/*.xml       → legacy comment XML
///     Files larger than MaxInlineFileSizeBytes are skipped.
///
///   Path B — SharePoint file-level comments (existing behaviour):
///     driveItem/comments API — the Comments sidebar in SharePoint/OneDrive web UI.
///     Works for all file types, no download required.
///
/// Project context is inferred from the folder path (parentReference.path).
/// Artifact name is the file name.
///
/// Required Graph scope: Files.Read
/// </summary>
public sealed class DriveExtractor : IDriveExtractor
{
    private readonly ILogger<DriveExtractor> _logger;
    private readonly HttpClient _http;

    private const string GraphV1 = "https://graph.microsoft.com/v1.0";
    private const long MaxInlineFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] OfficeExtensions = [".docx", ".xlsx", ".pptx"];

    // Action signals shared with ChatExtractor
    private static readonly string[] ActionSignals =
    [
        "i'll", "i will", "will do", "will send", "will review", "will fix",
        "will submit", "will complete", "by tomorrow", "by friday", "by eod",
        "by monday", "by next week", "i can", "let me", "i'll get",
        "on it", "taking this", "i own", "i'll own", "action:", "todo:", "follow up",
        "can you", "could you", "please", "action item"
    ];

    public DriveExtractor(
        ILogger<DriveExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _http   = httpClientFactory.CreateClient("graph");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string userId,
        string bearerToken,
        int days = 3,
        CancellationToken ct = default)
    {
        var since       = DateTimeOffset.UtcNow.AddDays(-days);
        var commitments = new List<RawCommitment>();

        // Fetch recently changed Office files via OneDrive delta
        var deltaUrl = $"{GraphV1}/me/drive/root/delta" +
                       "?$top=20&$select=name,file,webUrl,lastModifiedDateTime,parentReference," +
                       "id,remoteItem,createdBy,lastModifiedBy,size";

        var items = await GetPagedAsync(deltaUrl, bearerToken, ct);

        foreach (var item in items)
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null) continue;

            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (!OfficeExtensions.Contains(ext)) continue;

            if (!item.TryGetProperty("lastModifiedDateTime", out var lmdt)) continue;
            if (!DateTimeOffset.TryParse(lmdt.GetString(), out var modifiedAt)) continue;
            if (modifiedAt < since) continue;

            var fileId     = item.TryGetProperty("id",     out var fid) ? fid.GetString() : null;
            var webUrl     = item.TryGetProperty("webUrl", out var wu)  ? wu.GetString()  : "";
            var fileSize   = item.TryGetProperty("size",   out var sz)  ? sz.GetInt64()   : long.MaxValue;
            if (fileId is null) continue;

            // Infer project context from folder path
            var projectContext = InferProjectContext(item);
            var artifactName   = name;

            // ── Is the current user the author or last editor? ────────────────
            var createdById      = item.TryGetProperty("createdBy",      out var cb)
                ? cb.TryGetProperty("user", out var cbu)
                    ? cbu.TryGetProperty("id", out var cbid) ? cbid.GetString() : null : null : null;
            var lastModifiedById = item.TryGetProperty("lastModifiedBy", out var lb)
                ? lb.TryGetProperty("user", out var lbu)
                    ? lbu.TryGetProperty("id", out var lbid) ? lbid.GetString() : null : null : null;

            var isAuthoredByUser = string.Equals(createdById,      userId, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(lastModifiedById, userId, StringComparison.OrdinalIgnoreCase);

            // ── Path A: inline comments for files authored by the user ────────
            if (isAuthoredByUser && fileSize <= MaxInlineFileSizeBytes)
            {
                try
                {
                    var inlineComments = await ExtractInlineCommentsAsync(
                        fileId, ext, bearerToken, projectContext, artifactName, webUrl ?? "", ct);
                    commitments.AddRange(inlineComments);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DriveExtractor: inline comment parse failed for {Name}", name);
                }
            }

            // ── Path B: SharePoint file-level comments (all files) ────────────
            var commentsUrl = $"{GraphV1}/me/drive/items/{fileId}/comments";
            var comments    = await GetPagedAsync(commentsUrl, bearerToken, ct);

            foreach (var comment in comments)
            {
                var body = comment.TryGetProperty("content", out var cb2) ? cb2.GetString() : null;
                if (body is null || !HasActionSignal(body)) continue;

                var authorId   = ExtractAuthorId(comment);
                var authorName = ExtractAuthorName(comment);
                var commentId  = comment.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                var context    = body.Length > 200 ? body[..200] : body;
                var sourceMeta = commentId is not null
                    ? JsonSerializer.Serialize(new { itemId = fileId, commentId })
                    : null;

                commitments.Add(new RawCommitment(
                    Title:          InferTitle(body),
                    OwnerUserId:    authorId ?? "unknown",
                    OwnerDisplayName: authorName,
                    SourceType:     CommitmentSourceType.Drive,
                    SourceUrl:      webUrl ?? "",
                    ExtractedAt:    DateTimeOffset.UtcNow,
                    DueAt:          InferDueDate(body),
                    Confidence:     0.65,
                    WatcherUserIds: [],
                    SourceContext:  context,
                    SourceMetadata: sourceMeta,
                    ProjectContext: projectContext,
                    ArtifactName:   artifactName));
            }
        }

        _logger.LogInformation("DriveExtractor: {Count} raw commitments from last {Days}d", commitments.Count, days);
        return commitments;
    }

    // ── Path A: inline comment parsing ───────────────────────────────────────

    private async Task<List<RawCommitment>> ExtractInlineCommentsAsync(
        string fileId,
        string ext,
        string bearerToken,
        string? projectContext,
        string artifactName,
        string webUrl,
        CancellationToken ct)
    {
        // Download file content — Graph returns 302 to a pre-authenticated CDN URL
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{GraphV1}/me/drive/items/{fileId}/content");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode) return [];

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var zip          = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        return ext switch
        {
            ".docx" => ParseDocxComments(zip, fileId, webUrl, projectContext, artifactName),
            ".xlsx" => ParseXlsxComments(zip, fileId, webUrl, projectContext, artifactName),
            ".pptx" => ParsePptxComments(zip, fileId, webUrl, projectContext, artifactName),
            _       => []
        };
    }

    private List<RawCommitment> ParseDocxComments(
        ZipArchive zip, string fileId, string webUrl,
        string? projectContext, string artifactName)
    {
        var entry = zip.GetEntry("word/comments.xml");
        if (entry is null) return [];

        using var stream = entry.Open();
        var xml  = XDocument.Load(stream);
        var ns   = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var results = new List<RawCommitment>();

        foreach (var comment in xml.Descendants(XName.Get("comment", ns)))
        {
            var author = comment.Attribute(XName.Get("author", ns))?.Value ?? "Unknown";
            // Collect all text runs in the comment paragraph(s)
            var text = string.Concat(
                comment.Descendants(XName.Get("t", ns)).Select(t => t.Value));

            if (string.IsNullOrWhiteSpace(text) || !HasActionSignal(text)) continue;

            var context = text.Length > 200 ? text[..200] : text;
            results.Add(MakeCommitment(text, author, fileId, webUrl,
                projectContext, artifactName, context, isInline: true));
        }

        return results;
    }

    private List<RawCommitment> ParseXlsxComments(
        ZipArchive zip, string fileId, string webUrl,
        string? projectContext, string artifactName)
    {
        var results = new List<RawCommitment>();
        var ns      = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        // Excel can have multiple comment files: xl/comments1.xml, xl/comments2.xml …
        var commentEntries = zip.Entries
            .Where(e => e.FullName.StartsWith("xl/comments", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in commentEntries)
        {
            using var stream = entry.Open();
            var xml      = XDocument.Load(stream);
            var authors  = xml.Descendants(XName.Get("author", ns))
                              .Select(a => a.Value).ToList();

            foreach (var comment in xml.Descendants(XName.Get("comment", ns)))
            {
                var authorIdAttr = comment.Attribute("authorId");
                var author = authorIdAttr is not null
                    && int.TryParse(authorIdAttr.Value, out var idx)
                    && idx < authors.Count
                    ? authors[idx] : "Unknown";

                var text = string.Concat(
                    comment.Descendants(XName.Get("t", ns)).Select(t => t.Value));

                if (string.IsNullOrWhiteSpace(text) || !HasActionSignal(text)) continue;

                var context = text.Length > 200 ? text[..200] : text;
                results.Add(MakeCommitment(text, author, fileId, webUrl,
                    projectContext, artifactName, context, isInline: true));
            }
        }

        return results;
    }

    private List<RawCommitment> ParsePptxComments(
        ZipArchive zip, string fileId, string webUrl,
        string? projectContext, string artifactName)
    {
        var results = new List<RawCommitment>();

        // Modern format: ppt/comments/comment{N}.json (Office 365)
        var jsonEntries = zip.Entries
            .Where(e => e.FullName.StartsWith("ppt/comments/", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in jsonEntries)
        {
            using var stream = entry.Open();
            try
            {
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                // Each JSON file is one comment
                var text = ExtractPptxCommentText(root);
                if (string.IsNullOrWhiteSpace(text) || !HasActionSignal(text)) continue;

                var author = root.TryGetProperty("createdBy", out var cb)
                    ? cb.TryGetProperty("user", out var u)
                        ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "Unknown" : "Unknown"
                        : "Unknown"
                    : "Unknown";

                var context = text.Length > 200 ? text[..200] : text;
                results.Add(MakeCommitment(text, author, fileId, webUrl,
                    projectContext, artifactName, context, isInline: true));
            }
            catch { /* malformed JSON — skip this comment entry */ }
        }

        // Legacy format: ppt/comments/comment{N}.xml
        var xmlEntries = zip.Entries
            .Where(e => e.FullName.StartsWith("ppt/comments/", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in xmlEntries)
        {
            using var stream = entry.Open();
            try
            {
                var xml = XDocument.Load(stream);
                var ns  = "http://schemas.openxmlformats.org/presentationml/2006/main";

                // Build author list from <cmAuthorLst>
                var authorList = xml.Descendants(XName.Get("cmAuthor", ns))
                    .ToDictionary(
                        a => a.Attribute("id")?.Value ?? "",
                        a => a.Attribute("name")?.Value ?? "Unknown");

                foreach (var cm in xml.Descendants(XName.Get("cm", ns)))
                {
                    var authorId = cm.Attribute("authorId")?.Value ?? "";
                    var author   = authorList.GetValueOrDefault(authorId, "Unknown");
                    var textEl   = cm.Element(XName.Get("text", ns));
                    var text     = textEl?.Value ?? "";

                    if (string.IsNullOrWhiteSpace(text) || !HasActionSignal(text)) continue;

                    var context = text.Length > 200 ? text[..200] : text;
                    results.Add(MakeCommitment(text, author, fileId, webUrl,
                        projectContext, artifactName, context, isInline: true));
                }
            }
            catch { /* malformed XML — skip */ }
        }

        return results;
    }

    private static string ExtractPptxCommentText(JsonElement root)
    {
        // Modern PPTX JSON: {"text":{"runs":[{"text":"…"},{"text":"…"}]}}
        if (!root.TryGetProperty("text", out var textEl)) return "";
        if (!textEl.TryGetProperty("runs", out var runs)) return "";
        return string.Concat(runs.EnumerateArray()
            .Select(r => r.TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""));
    }

    private RawCommitment MakeCommitment(
        string text, string author, string fileId,
        string webUrl, string? projectContext, string artifactName,
        string context, bool isInline)
    {
        return new RawCommitment(
            Title:            InferTitle(text),
            OwnerUserId:      author,    // will be resolved to OID downstream if possible
            OwnerDisplayName: author,
            SourceType:       CommitmentSourceType.Drive,
            SourceUrl:        webUrl,
            ExtractedAt:      DateTimeOffset.UtcNow,
            DueAt:            InferDueDate(text),
            Confidence:       isInline ? 0.72 : 0.65,   // inline = higher confidence
            WatcherUserIds:   [],
            SourceContext:    context,
            SourceMetadata:   JsonSerializer.Serialize(new { itemId = fileId, commentId = (string?)null }),
            ProjectContext:   projectContext,
            ArtifactName:     artifactName);
    }

    // ── Project context + helpers ─────────────────────────────────────────────

    private static string? InferProjectContext(JsonElement item)
    {
        if (!item.TryGetProperty("parentReference", out var parentRef)) return null;
        var path = parentRef.TryGetProperty("path", out var p) ? p.GetString() : null;
        if (path is null) return null;

        // Path format: /drive/root:/Folder/SubFolder  OR  /drive/root:
        var colonSlash = path.IndexOf(":/", StringComparison.Ordinal);
        var relative   = colonSlash >= 0 ? path[(colonSlash + 2)..] : path;
        if (string.IsNullOrWhiteSpace(relative)) return "OneDrive";

        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : "OneDrive";
    }

    private static bool HasActionSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ActionSignals.Any(s => lower.Contains(s));
    }

    private static string InferTitle(string text)
    {
        var end = text.IndexOfAny(['.', '!', '\n'], 0);
        var raw = (end > 0 ? text[..end] : text).Trim();
        return raw.Length > 80 ? raw[..80] + "…" : raw;
    }

    private static DateTimeOffset? InferDueDate(string text)
    {
        var lower = text.ToLowerInvariant();
        var now   = DateTimeOffset.UtcNow;
        if (lower.Contains("by eod") || lower.Contains("today"))   return now.Date.AddHours(18);
        if (lower.Contains("tomorrow"))                             return now.AddDays(1).Date.AddHours(18);
        if (lower.Contains("by friday") || lower.Contains("end of week"))
        {
            var d = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(d).Date.AddHours(18);
        }
        if (lower.Contains("next week") || lower.Contains("by monday"))
            return now.AddDays(7 - (int)now.DayOfWeek + 1).Date.AddHours(9);
        return null;
    }

    private static string? ExtractAuthorId(JsonElement comment)
    {
        var author = comment.TryGetProperty("author", out var a) ? a : default;
        if (author.ValueKind == JsonValueKind.Undefined) return null;
        return author.TryGetProperty("user", out var u)
            ? u.TryGetProperty("id", out var uid) ? uid.GetString() : null
            : null;
    }

    private static string ExtractAuthorName(JsonElement comment)
    {
        var author = comment.TryGetProperty("author", out var a) ? a : default;
        if (author.ValueKind == JsonValueKind.Undefined) return "Unknown";
        return author.TryGetProperty("user", out var u)
            ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "Unknown" : "Unknown"
            : "Unknown";
    }

    private async Task<List<JsonElement>> GetPagedAsync(
        string url, string bearerToken, CancellationToken ct)
    {
        var results  = new List<JsonElement>();
        string? next = url;

        while (next is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, next);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) break;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var value))
                foreach (var item in value.EnumerateArray())
                    results.Add(item.Clone());

            next = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? nl.GetString() : null;
        }

        return results;
    }
}
