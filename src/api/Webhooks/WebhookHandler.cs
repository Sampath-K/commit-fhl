using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommitApi.Config;
using CommitApi.Services;

namespace CommitApi.Webhooks;

/// <summary>
/// Handles incoming Microsoft Graph change notification webhook payloads.
///
/// When a notification arrives for a user whose OBO token is in the TokenCache,
/// this handler immediately triggers extraction — giving &lt; 30s latency for Teams
/// messages and Outlook emails (vs the 5-minute polling cycle).
///
/// Flow:
///   1. Validate HMAC-SHA256 signature (anti-tampering)
///   2. Validate clientState (anti-replay)
///   3. Resolve subscriptionId → userId via TokenCache
///   4. If user has a cached token → trigger ExtractionOrchestrator immediately
///   5. Emit telemetry
/// </summary>
public sealed class WebhookHandler
{
    private readonly ILogger<WebhookHandler>   _logger;
    private readonly IAppInsightsClient        _insights;
    private readonly string                    _clientState;
    private readonly TokenCache                _tokenCache;
    private readonly IExtractionOrchestrator   _orchestrator;

    public WebhookHandler(
        ILogger<WebhookHandler>   logger,
        IAppInsightsClient        insights,
        string                    clientState,
        TokenCache                tokenCache,
        IExtractionOrchestrator   orchestrator)
    {
        _logger       = logger;
        _insights     = insights;
        _clientState  = clientState;
        _tokenCache   = tokenCache;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Validates the HMAC-SHA256 signature on an incoming webhook payload.
    /// Microsoft Graph signs the raw body with the subscription's client state as the key.
    /// </summary>
    public bool ValidateSignature(byte[] rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        var keyBytes    = Encoding.UTF8.GetBytes(_clientState);
        var expected    = HMACSHA256.HashData(keyBytes, rawBody);
        var expectedB64 = Convert.ToBase64String(expected);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedB64),
            Encoding.UTF8.GetBytes(signatureHeader));
    }

    /// <summary>
    /// Processes a validated Graph change notification payload.
    /// For each notification, resolves the user and triggers extraction if a token is available.
    /// </summary>
    public async Task HandleAsync(string body, CancellationToken ct = default)
    {
        var payload = JsonDocument.Parse(body);
        var notifications = payload.RootElement
            .GetProperty("value")
            .EnumerateArray();

        // Deduplicate: one extraction per user even if multiple notifications arrive in the same batch
        var usersToExtract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var notification in notifications)
        {
            var subscriptionId = notification.TryGetProperty("subscriptionId", out var sid)
                ? sid.GetString() : "unknown";
            var changeType = notification.TryGetProperty("changeType", out var ct2)
                ? ct2.GetString() : "unknown";
            var resource = notification.TryGetProperty("resource", out var res)
                ? res.GetString() : "unknown";
            var tenantId = notification.TryGetProperty("tenantId", out var tid)
                ? tid.GetString() : "unknown";

            // Validate clientState (anti-replay)
            var incomingState = notification.TryGetProperty("clientState", out var cs)
                ? cs.GetString() : null;

            if (incomingState != _clientState)
            {
                _logger.LogWarning(
                    "Webhook clientState mismatch — subscription {SubscriptionId}. Ignoring.",
                    subscriptionId);
                continue;
            }

            _logger.LogInformation(
                "Graph webhook: {ChangeType} on {Resource} (subscription {SubscriptionId})",
                changeType, resource, subscriptionId);

            _insights.TrackBusinessKpi(
                kpiType:      "webhook-received",
                hashedUserId: PiiScrubber.HashValue(tenantId ?? ""),
                count:        1,
                properties:   new Dictionary<string, string>
                {
                    ["changeType"]     = changeType ?? "",
                    ["subscriptionId"] = subscriptionId ?? "",
                });

            // Resolve userId from subscriptionId → trigger extraction
            if (subscriptionId is not null)
            {
                var userId = _tokenCache.GetUserIdForSubscription(subscriptionId);
                if (userId is not null)
                    usersToExtract.Add(userId);
            }
        }

        // Trigger extraction for each unique user in this webhook batch
        if (usersToExtract.Count > 0)
        {
            _logger.LogInformation(
                "Webhook: triggering immediate extraction for {Count} user(s)", usersToExtract.Count);

            var extractTasks = usersToExtract.Select(async userId =>
            {
                var entry = _tokenCache.Get(userId);
                if (entry is null)
                {
                    _logger.LogDebug(
                        "Webhook: no valid token for user {Hash} — skipping immediate extraction",
                        PiiScrubber.HashValue(userId));
                    return;
                }

                try
                {
                    var count = await _orchestrator.ExtractAndStoreAsync(userId, entry.Token, ct);
                    _tokenCache.MarkExtracted(userId);

                    _logger.LogInformation(
                        "Webhook-triggered extraction: {Count} new item(s) for user {Hash}",
                        count, PiiScrubber.HashValue(userId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Webhook-triggered extraction failed for user {Hash}", PiiScrubber.HashValue(userId));
                }
            });

            await Task.WhenAll(extractTasks);
        }
    }
}
