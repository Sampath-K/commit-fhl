using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace CommitApi.Services;

/// <summary>
/// In-memory cache of user OBO tokens, keyed by AAD Object ID (userId).
///
/// Tokens are stored when a user makes an authenticated request (/extract, /commitments, /subscriptions).
/// Background services (ExtractionPollingService, WebhookHandler) read from this cache so they
/// can run Graph API calls on behalf of users without requiring an active HTTP session.
///
/// OBO tokens expire after ~1 hour. We store the token's actual `exp` claim and skip users
/// whose tokens have expired. The user refreshes their token naturally on next app open.
///
/// Also stores: subscriptionId → userId mapping for webhook-triggered extraction.
/// </summary>
public sealed class TokenCache
{
    private readonly ConcurrentDictionary<string, UserTokenEntry> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _subscriptionToUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<TokenCache> _logger;

    public TokenCache(ILogger<TokenCache> logger) => _logger = logger;

    // ── Token management ─────────────────────────────────────────────────────

    /// <summary>
    /// Stores or refreshes the OBO token for a user.
    /// Called from authenticated endpoints whenever a fresh Bearer token is received.
    /// </summary>
    public void Store(string userId, string bearerToken)
    {
        var expiry = ExtractExpiry(bearerToken);
        var entry = _tokens.AddOrUpdate(
            userId,
            _ => new UserTokenEntry(userId, bearerToken, expiry, null),
            (_, existing) => existing with { Token = bearerToken, ExpiresAt = expiry }
        );
        _logger.LogDebug("TokenCache: stored token for user {Hash} (expires {Expiry})",
            userId[..Math.Min(8, userId.Length)], expiry);
    }

    /// <summary>Gets the stored token entry for a user, or null if not cached / expired.</summary>
    public UserTokenEntry? Get(string userId)
    {
        if (!_tokens.TryGetValue(userId, out var entry)) return null;
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2))
        {
            _tokens.TryRemove(userId, out _);
            return null;
        }
        return entry;
    }

    /// <summary>Returns all userIds with non-expired tokens.</summary>
    public IEnumerable<string> GetActiveUserIds() =>
        _tokens.Values
               .Where(e => e.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
               .Select(e => e.UserId);

    /// <summary>Updates the LastExtracted timestamp after a successful extraction run.</summary>
    public void MarkExtracted(string userId)
    {
        if (_tokens.TryGetValue(userId, out var entry))
            _tokens[userId] = entry with { LastExtracted = DateTimeOffset.UtcNow };
    }

    // ── Subscription mapping ─────────────────────────────────────────────────

    /// <summary>
    /// Associates a Graph subscription ID with the user who owns it.
    /// Called by SubscriptionManager after each successful subscription creation.
    /// </summary>
    public void MapSubscription(string subscriptionId, string userId)
    {
        _subscriptionToUser[subscriptionId] = userId;
        _logger.LogDebug("TokenCache: mapped subscription {SubscriptionId} → user {Hash}",
            subscriptionId[..Math.Min(8, subscriptionId.Length)],
            userId[..Math.Min(8, userId.Length)]);
    }

    /// <summary>Resolves a subscriptionId to its owning userId, or null if unknown.</summary>
    public string? GetUserIdForSubscription(string subscriptionId) =>
        _subscriptionToUser.TryGetValue(subscriptionId, out var userId) ? userId : null;

    // ── JWT expiry extraction ────────────────────────────────────────────────

    private DateTimeOffset ExtractExpiry(string bearerToken)
    {
        try
        {
            // JWT = header.payload.signature — base64url-decode the payload
            var parts = bearerToken.Split('.');
            if (parts.Length < 2) throw new FormatException("Not a JWT");

            var padding = (4 - parts[1].Length % 4) % 4;
            var base64 = parts[1].Replace('-', '+').Replace('_', '/') + new string('=', padding);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expEl))
            {
                var unixSeconds = expEl.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("TokenCache: could not parse JWT expiry ({Msg}) — defaulting to 55 min", ex.Message);
        }

        // Safe default: assume 55-minute token (standard AAD OBO TTL)
        return DateTimeOffset.UtcNow.AddMinutes(55);
    }

    /// <summary>
    /// Extracts the AAD Object ID (oid claim) from a Bearer token.
    /// Returns null if the token is not a valid JWT or oid is missing.
    /// </summary>
    public static string? ExtractUserId(string bearerToken)
    {
        try
        {
            var parts = bearerToken.Split('.');
            if (parts.Length < 2) return null;

            var padding = (4 - parts[1].Length % 4) % 4;
            var base64 = parts[1].Replace('-', '+').Replace('_', '/') + new string('=', padding);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("oid", out var oid) ? oid.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Immutable token state for one user.</summary>
public sealed record UserTokenEntry(
    string          UserId,
    string          Token,
    DateTimeOffset  ExpiresAt,
    DateTimeOffset? LastExtracted
);
