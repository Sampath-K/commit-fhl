using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Scans Teams DMs and channel messages for action-intent signals
/// that indicate commitments ("I'll", "will do", "by Friday", etc.).
/// </summary>
public sealed class ChatExtractor : IChatExtractor
{
    private readonly ILogger<ChatExtractor> _logger;
    private readonly HttpClient _http;

    private const string GraphV1 = "https://graph.microsoft.com/v1.0";

    // Action-intent phrases that indicate a commitment
    private static readonly string[] ActionSignals =
    [
        "i'll", "i will", "will do", "will send", "will review", "will fix",
        "will submit", "will complete", "by tomorrow", "by friday", "by eod",
        "by monday", "by next week", "i can", "let me", "i'll get",
        "on it", "taking this", "i own", "i'll own"
    ];

    public ChatExtractor(
        ILogger<ChatExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _http   = httpClientFactory.CreateClient("graph");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 3,
        CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days).ToString("o");
        var commitments = new List<RawCommitment>();

        // Fetch the user's chats (DMs and group chats)
        var chatsUrl = $"{GraphV1}/me/chats?$select=id,chatType,topic&$top=20";
        var chats    = await GetPagedAsync(chatsUrl, bearerToken, ct);

        foreach (var chat in chats)
        {
            var chatId   = chat.GetProperty("id").GetString() ?? "";
            var chatType = chat.TryGetProperty("chatType", out var ct2) ? ct2.GetString() : "unknown";
            var topic    = chat.TryGetProperty("topic", out var t) ? t.GetString() : null;

            // Only DMs and small group chats (not channels — handled separately)
            if (chatType != "oneOnOne" && chatType != "group") continue;

            var messagesUrl = $"{GraphV1}/me/chats/{chatId}/messages" +
                              $"?$filter=createdDateTime ge {Uri.EscapeDataString(since)}" +
                              $"&$select=id,from,body,createdDateTime,webUrl&$top=50";

            var messages = await GetPagedAsync(messagesUrl, bearerToken, ct);

            foreach (var msg in messages)
            {
                var bodyContent = msg.TryGetProperty("body", out var body)
                    ? body.TryGetProperty("content", out var c) ? c.GetString() : null
                    : null;

                if (bodyContent is null || !HasActionSignal(bodyContent)) continue;

                var sender    = msg.TryGetProperty("from", out var from)
                    ? from.TryGetProperty("user", out var u) ? u : default
                    : default;
                var userId    = sender.ValueKind != JsonValueKind.Undefined
                    ? sender.TryGetProperty("id", out var uid) ? uid.GetString() : null
                    : null;
                var name      = sender.ValueKind != JsonValueKind.Undefined
                    ? sender.TryGetProperty("displayName", out var dn) ? dn.GetString() : "Unknown"
                    : "Unknown";
                var webUrl    = msg.TryGetProperty("webUrl", out var wu) ? wu.GetString() : "";
                var createdAt = msg.TryGetProperty("createdDateTime", out var cr)
                    ? DateTimeOffset.Parse(cr.GetString()!)
                    : DateTimeOffset.UtcNow;

                // Strip HTML tags from body for context snippet
                var plainText = StripHtml(bodyContent);
                var context   = plainText.Length > 200 ? plainText[..200] : plainText;

                commitments.Add(new RawCommitment(
                    Title:             InferTitle(plainText),
                    OwnerUserId:       userId ?? "unknown",
                    OwnerDisplayName:  name ?? "Unknown",
                    SourceType:        CommitmentSourceType.Chat,
                    SourceUrl:         webUrl ?? "",
                    ExtractedAt:       DateTimeOffset.UtcNow,
                    DueAt:             InferDueDate(plainText),
                    Confidence:        0.70,  // Chat signals are heuristic; NLP pipeline will refine
                    WatcherUserIds:    [],
                    SourceContext:     context));
            }
        }

        _logger.LogInformation("Chat extractor: {Count} raw commitments from last {Days}d", commitments.Count, days);
        return commitments;
    }

    private static bool HasActionSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ActionSignals.Any(signal => lower.Contains(signal));
    }

    private static string InferTitle(string text)
    {
        // Take first sentence (up to 80 chars) as the title
        var end = text.IndexOfAny(['.', '!', '\n'], 0);
        var raw = end > 0 ? text[..end] : text;
        raw = raw.Trim();
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
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(daysUntilFriday).Date.AddHours(18);
        }
        if (lower.Contains("next week") || lower.Contains("by monday"))
            return now.AddDays(7 - (int)now.DayOfWeek + 1).Date.AddHours(9);
        return null;
    }

    private static string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Trim();
    }

    private async Task<List<JsonElement>> GetPagedAsync(
        string url,
        string bearerToken,
        CancellationToken ct)
    {
        var results = new List<JsonElement>();
        string? nextLink = url;

        while (nextLink is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, nextLink);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await _http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode) break;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var value))
                foreach (var item in value.EnumerateArray())
                    results.Add(item.Clone());

            nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? nl.GetString()
                : null;
        }

        return results;
    }
}
