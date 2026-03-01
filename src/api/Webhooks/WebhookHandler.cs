using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommitApi.Config;
using Microsoft.Extensions.Primitives;

namespace CommitApi.Webhooks;

/// <summary>
/// Handles incoming Microsoft Graph change notification webhook payloads.
/// Validates HMAC-SHA256 signatures before processing any payload.
/// </summary>
public sealed class WebhookHandler
{
    private readonly ILogger<WebhookHandler> _logger;
    private readonly IAppInsightsClient _insights;
    private readonly string _clientState;

    // Graph sends the signature in this header
    private const string SignatureHeader = "X-Microsoft-Gryffindor-Signature";

    public WebhookHandler(
        ILogger<WebhookHandler> logger,
        IAppInsightsClient insights,
        string clientState)
    {
        _logger      = logger;
        _insights    = insights;
        _clientState = clientState;
    }

    /// <summary>
    /// Validates the HMAC-SHA256 signature on an incoming webhook payload.
    /// Microsoft Graph signs the raw body with the subscription's client state as the key.
    /// </summary>
    /// <param name="rawBody">Raw UTF-8 request body bytes.</param>
    /// <param name="signatureHeader">Value of X-Microsoft-Gryffindor-Signature header.</param>
    /// <returns>True if the signature is valid; false if tampered or missing.</returns>
    public bool ValidateSignature(byte[] rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        var keyBytes  = Encoding.UTF8.GetBytes(_clientState);
        var expected  = HMACSHA256.HashData(keyBytes, rawBody);
        var expectedB64 = Convert.ToBase64String(expected);

        // Constant-time compare to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedB64),
            Encoding.UTF8.GetBytes(signatureHeader));
    }

    /// <summary>
    /// Processes a validated Graph change notification payload.
    /// Logs each notification and emits a telemetry event.
    /// </summary>
    /// <param name="body">Validated JSON body of the webhook notification.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(string body, CancellationToken ct = default)
    {
        var payload = JsonDocument.Parse(body);
        var notifications = payload.RootElement
            .GetProperty("value")
            .EnumerateArray();

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
        }

        await Task.CompletedTask;
    }
}
