using CommitApi.Auth;
using Microsoft.Graph.Models;

namespace CommitApi.Services;

/// <summary>
/// Background service that renews expiring Microsoft Graph change notification subscriptions.
///
/// Runs every 6 hours. For each user with a valid cached token, it fetches their Graph
/// subscriptions and renews any that will expire within the next 24 hours.
///
/// Without this service, subscriptions created at startup expire silently after ~23 hours,
/// causing Teams and email change notifications to stop arriving.
/// </summary>
public sealed class SubscriptionRenewalService : BackgroundService
{
    private readonly TokenCache _tokenCache;
    private readonly IGraphClientFactory _graphFactory;
    private readonly ILogger<SubscriptionRenewalService> _logger;

    private static readonly TimeSpan RenewalInterval   = TimeSpan.FromHours(6);
    private static readonly TimeSpan RenewalThreshold  = TimeSpan.FromHours(24);
    private static readonly TimeSpan NewLifetime        = TimeSpan.FromHours(23);

    public SubscriptionRenewalService(
        TokenCache tokenCache,
        IGraphClientFactory graphFactory,
        ILogger<SubscriptionRenewalService> logger)
    {
        _tokenCache   = tokenCache;
        _graphFactory = graphFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("SubscriptionRenewalService: started (interval={Interval}h)", RenewalInterval.TotalHours);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(RenewalInterval, ct);

            var activeUsers = _tokenCache.GetActiveUserIds().ToList();
            _logger.LogInformation("SubscriptionRenewalService: checking {Count} active user(s)", activeUsers.Count);

            var totalRenewed = 0;

            foreach (var userId in activeUsers)
            {
                var entry = _tokenCache.Get(userId);
                if (entry is null) continue;

                try
                {
                    totalRenewed += await RenewSubscriptionsForUserAsync(entry.Token, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "SubscriptionRenewalService: renewal failed for user {Hash}",
                        userId[..Math.Min(8, userId.Length)]);
                }
            }

            _logger.LogInformation("SubscriptionRenewalService: renewed {Count} subscription(s)", totalRenewed);
        }
    }

    private async Task<int> RenewSubscriptionsForUserAsync(string bearerToken, CancellationToken ct)
    {
        var client = _graphFactory.CreateOnBehalfOf(bearerToken);
        var subs   = await client.Subscriptions.GetAsync(cancellationToken: ct);

        if (subs?.Value is null or { Count: 0 }) return 0;

        var threshold = DateTimeOffset.UtcNow.Add(RenewalThreshold);
        var renewed   = 0;

        foreach (var sub in subs.Value)
        {
            if (sub.Id is null) continue;
            if (sub.ExpirationDateTime is null || sub.ExpirationDateTime > threshold) continue;

            try
            {
                await client.Subscriptions[sub.Id].PatchAsync(new Subscription
                {
                    ExpirationDateTime = DateTimeOffset.UtcNow.Add(NewLifetime),
                }, cancellationToken: ct);

                _logger.LogInformation(
                    "SubscriptionRenewalService: renewed subscription {SubId} (resource={Resource})",
                    sub.Id[..Math.Min(8, sub.Id.Length)], sub.Resource);

                renewed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SubscriptionRenewalService: failed to renew subscription {SubId}", sub.Id);
            }
        }

        return renewed;
    }
}
