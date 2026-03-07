using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Extractors.Helpers;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Fetches unread or flagged Outlook emails and surfaces commitment signals.
/// </summary>
public sealed class EmailExtractor : IEmailExtractor
{
    private readonly ILogger<EmailExtractor> _logger;
    private readonly HttpClient _http;

    private const string GraphV1 = "https://graph.microsoft.com/v1.0";

    public EmailExtractor(
        ILogger<EmailExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _http   = httpClientFactory.CreateClient("graph");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default)
    {
        var since       = DateTimeOffset.UtcNow.AddDays(-days).ToString("o");
        var commitments = new List<RawCommitment>();

        // Fetch unread + flagged emails from inbox — body preview is 255 chars (P-09 compliant)
        var url = $"{GraphV1}/me/mailFolders/inbox/messages" +
                  $"?$filter=(isRead eq false or flag/flagStatus eq 'flagged')" +
                  $" and receivedDateTime ge {Uri.EscapeDataString(since)}" +
                  $"&$select=id,subject,from,receivedDateTime,bodyPreview,webLink,flag,conversationId&$top=50";

        string? nextLink = url;

        while (nextLink is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, nextLink);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await _http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Email extractor: Graph returned {Status}", resp.StatusCode);
                break;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            foreach (var msg in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var subject        = msg.TryGetProperty("subject", out var s) ? s.GetString() : "";
                var preview        = msg.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() ?? "" : "";
                var webLink        = msg.TryGetProperty("webLink", out var wl) ? wl.GetString() ?? "" : "";
                var messageId      = msg.TryGetProperty("id", out var mid) ? mid.GetString() : null;
                var conversationId = msg.TryGetProperty("conversationId", out var cid) ? cid.GetString() : null;
                var receivedAt     = msg.TryGetProperty("receivedDateTime", out var rd)
                    ? DateTimeOffset.Parse(rd.GetString()!)
                    : DateTimeOffset.UtcNow;

                var from        = msg.TryGetProperty("from", out var f)
                    ? f.TryGetProperty("emailAddress", out var ea) ? ea : default
                    : default;
                var senderName  = from.ValueKind != JsonValueKind.Undefined
                    ? from.TryGetProperty("name", out var n) ? n.GetString() : "Unknown"
                    : "Unknown";

                if (!HasActionSignal(subject + " " + preview)) continue;

                var context    = preview.Length > 200 ? preview[..200] : preview;
                var sourceMeta = messageId is not null
                    ? System.Text.Json.JsonSerializer.Serialize(new { messageId, conversationId })
                    : null;

                commitments.Add(new RawCommitment(
                    Title:            NormalizeSubject(subject ?? "Action required"),
                    OwnerUserId:      "me",  // email sender is the watcher; "me" is implicitly the owner
                    OwnerDisplayName: "Current User",
                    SourceType:       CommitmentSourceType.Email,
                    SourceUrl:        webLink,
                    ExtractedAt:      DateTimeOffset.UtcNow,
                    DueAt:            InferDueDate(subject + " " + preview),
                    Confidence:       0.65,  // Email heuristic baseline; NLP refines
                    WatcherUserIds:   [],
                    SourceContext:    context,
                    SourceMetadata:   sourceMeta,
                    ArtifactName:     NormalizeSubject(subject ?? "")));
            }

            nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? nl.GetString()
                : null;
        }

        _logger.LogInformation("Email extractor: {Count} raw commitments from last {Days}d", commitments.Count, days);
        return commitments;
    }

    private static bool HasActionSignal(string text)           => EmailSignals.HasActionSignal(text);
    private static string NormalizeSubject(string subject)     => EmailSignals.NormalizeSubject(subject);
    private static DateTimeOffset? InferDueDate(string text)   => EmailSignals.InferDueDate(text);
}
