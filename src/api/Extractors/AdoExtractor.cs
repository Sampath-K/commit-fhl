using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Fetches Azure DevOps PR review threads and surfaces unresolved
/// review requests as commitment signals.
/// </summary>
public sealed class AdoExtractor : IAdoExtractor
{
    private readonly ILogger<AdoExtractor> _logger;
    private readonly HttpClient _http;
    private readonly string _adoOrg;
    private readonly string _adoProject;
    private readonly string? _adoPat;

    public AdoExtractor(
        ILogger<AdoExtractor> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _logger     = logger;
        _http       = httpClientFactory.CreateClient("ado");
        _adoOrg     = config["ADO_ORG"]     ?? Environment.GetEnvironmentVariable("ADO_ORG")     ?? "";
        _adoProject = config["ADO_PROJECT"]  ?? Environment.GetEnvironmentVariable("ADO_PROJECT")  ?? "";
        _adoPat     = config["ADO_PAT"]      ?? Environment.GetEnvironmentVariable("ADO_PAT");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string userId,
        string bearerToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_adoOrg) || string.IsNullOrEmpty(_adoProject))
        {
            _logger.LogInformation("ADO extractor skipped: ADO_ORG / ADO_PROJECT not configured");
            return [];
        }

        var commitments = new List<RawCommitment>();
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        // ── 1. Open PRs → unresolved review threads → commitments ─────────────
        var prsUrl = $"https://dev.azure.com/{_adoOrg}/{_adoProject}/_apis/git/pullrequests" +
                     $"?api-version=7.1&searchCriteria.status=active&$top=50";

        var prs = await GetAdoAsync(prsUrl, ct);
        if (prs is null) return [];

        foreach (var pr in prs.Value.EnumerateArray())
        {
            var prId         = pr.GetProperty("pullRequestId").GetInt32();
            var repoId       = pr.GetProperty("repository").GetProperty("id").GetString() ?? "";
            var prTitle      = pr.TryGetProperty("title", out var t) ? t.GetString() : $"PR #{prId}";
            var webUrl       = pr.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

            // Fetch threads for this PR
            var threadsUrl = $"https://dev.azure.com/{_adoOrg}/{_adoProject}/_apis/git/repositories/{repoId}" +
                             $"/pullRequests/{prId}/threads?api-version=7.1";

            var threadsDoc = await GetAdoAsync(threadsUrl, ct);
            if (threadsDoc is null) continue;

            foreach (var thread in threadsDoc.Value.EnumerateArray())
            {
                var status = thread.TryGetProperty("status", out var s) ? s.GetString() : "active";
                if (status != "active") continue;  // resolved / byDesign → not a commitment

                if (!thread.TryGetProperty("comments", out var comments)) continue;

                foreach (var comment in comments.EnumerateArray())
                {
                    var content = comment.TryGetProperty("content", out var c) ? c.GetString() : "";
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    if (!HasReviewSignal(content)) continue;

                    var author = comment.TryGetProperty("author", out var a)
                        ? a.TryGetProperty("uniqueName", out var un) ? un.GetString() : null
                        : null;
                    var displayName = comment.TryGetProperty("author", out var a2)
                        ? a2.TryGetProperty("displayName", out var dn) ? dn.GetString() : "Reviewer"
                        : "Reviewer";

                    var context = content.Length > 200 ? content[..200] : content;

                    commitments.Add(new RawCommitment(
                        Title:            $"Address PR review: {prTitle}",
                        OwnerUserId:      userId,  // the PR author is responsible for addressing reviews
                        OwnerDisplayName: displayName ?? "Reviewer",
                        SourceType:       CommitmentSourceType.Ado,
                        SourceUrl:        webUrl,
                        ExtractedAt:      DateTimeOffset.UtcNow,
                        DueAt:            null,
                        Confidence:       0.80,  // Unresolved review = strong commitment signal
                        WatcherUserIds:   author is not null ? [author] : [],
                        SourceContext:    context));
                }
            }
        }

        // ── 2. Recently merged PRs → completions ──────────────────────────────
        var mergedUrl = $"https://dev.azure.com/{_adoOrg}/{_adoProject}/_apis/git/pullrequests" +
                        $"?api-version=7.1&searchCriteria.status=completed&$top=50";

        var mergedPrs = await GetAdoAsync(mergedUrl, ct);
        if (mergedPrs is not null)
        {
            foreach (var pr in mergedPrs.Value.EnumerateArray())
            {
                // Only include PRs closed within the look-back window
                var closedDateStr = pr.TryGetProperty("closedDate", out var cd) ? cd.GetString() : null;
                if (closedDateStr is null ||
                    !DateTimeOffset.TryParse(closedDateStr, out var closedDate) ||
                    closedDate < since)
                    continue;

                var prId    = pr.GetProperty("pullRequestId").GetInt32();
                var prTitle = pr.TryGetProperty("title", out var t) ? t.GetString() ?? $"PR #{prId}" : $"PR #{prId}";
                var webUrl  = pr.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";

                var truncatedTitle = prTitle.Length > 80 ? prTitle[..80] + "…" : prTitle;

                commitments.Add(new RawCommitment(
                    Title:            $"Merged: {truncatedTitle}",
                    OwnerUserId:      userId,
                    OwnerDisplayName: "Me",
                    SourceType:       CommitmentSourceType.Ado,
                    SourceUrl:        webUrl,
                    ExtractedAt:      closedDate,
                    DueAt:            null,
                    Confidence:       1.0,
                    WatcherUserIds:   [],
                    SourceContext:    $"PR #{prId} merged {closedDate:MMM d}",
                    ItemKind:         ItemKind.Completion));
            }
        }

        _logger.LogInformation("ADO extractor: {Count} raw signals (commitments + completions)", commitments.Count);
        return commitments;
    }

    private static readonly string[] ReviewSignals =
    [
        "please fix", "please update", "can you", "could you", "nitpick",
        "blocking", "needs to be", "should be", "must be", "action:", "todo:", "fixme:"
    ];

    private static bool HasReviewSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ReviewSignals.Any(s => lower.Contains(s));
    }

    private async Task<JsonElement?> GetAdoAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(_adoPat))
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{_adoPat}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("ADO API {Url} returned {Status}", url, resp.StatusCode);
            return null;
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        var doc  = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
