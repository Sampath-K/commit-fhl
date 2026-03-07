using CommitApi.Auth;
using CommitApi.Exceptions;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CommitApi.Webhooks;

/// <summary>
/// Manages Microsoft Graph change notification subscriptions.
/// Creates subscriptions for Teams chat messages and Outlook inbox messages,
/// both of which are sources for commitment extraction (T-010, T-012, T-013).
/// </summary>
public sealed class SubscriptionManager : ISubscriptionManager
{
    private readonly IGraphClientFactory _graphFactory;
    private readonly ILogger<SubscriptionManager> _logger;
    private readonly string _notificationUrl;
    private readonly string _lifecycleNotificationUrl;
    private readonly string _clientState;

    // Subscription resources to monitor.
    // NeedsLifecycle: Graph requires a lifecycleNotificationUrl for chat subscriptions with expiry > 1h.
    private static readonly (string Resource, string ChangeTypes, string Description, bool NeedsLifecycle)[] Subscriptions =
    [
        ("/me/chats/getAllMessages",               "created,updated", "Teams chat messages",  true),
        ("/me/mailFolders/inbox/messages",         "created",         "Outlook inbox messages", false),
        ("/me/drive/root",                         "updated",         "OneDrive file changes",  false),
    ];

    // Graph subscriptions expire; we renew when < 1 day remains
    private static readonly TimeSpan SubscriptionLifetime = TimeSpan.FromHours(23);

    public SubscriptionManager(
        IGraphClientFactory graphFactory,
        ILogger<SubscriptionManager> logger,
        string notificationUrl,
        string clientState)
    {
        _graphFactory              = graphFactory;
        _logger                    = logger;
        _notificationUrl           = notificationUrl;
        _lifecycleNotificationUrl  = notificationUrl + "/lifecycle";
        _clientState               = clientState;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> EnsureSubscriptionsAsync(string bearerToken,
        CancellationToken ct = default)
    {
        var client = _graphFactory.CreateOnBehalfOf(bearerToken);
        var createdIds = new List<string>();

        foreach (var (resource, changeTypes, description, needsLifecycle) in Subscriptions)
        {
            try
            {
                var subscription = new Subscription
                {
                    ChangeType               = changeTypes,
                    NotificationUrl          = _notificationUrl,
                    Resource                 = resource,
                    ExpirationDateTime       = DateTimeOffset.UtcNow.Add(SubscriptionLifetime),
                    ClientState              = _clientState,
                    LifecycleNotificationUrl = needsLifecycle ? _lifecycleNotificationUrl : null,
                };

                var created = await client.Subscriptions.PostAsync(subscription, cancellationToken: ct);
                if (created?.Id is not null)
                {
                    createdIds.Add(created.Id);
                    _logger.LogInformation(
                        "Created Graph subscription for {Description}: {SubscriptionId}",
                        description, created.Id);
                }
            }
            catch (ServiceException ex) when (ex.ResponseStatusCode == 409)
            {
                // Subscription already exists — find and return existing ID
                _logger.LogInformation(
                    "Subscription already exists for {Description} — skipping creation", description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create subscription for {Description}", description);
                throw new GraphException($"Subscription creation failed for {description}", ex.Message);
            }
        }

        return createdIds;
    }

    /// <inheritdoc />
    public async Task DeleteAllSubscriptionsAsync(string bearerToken, CancellationToken ct = default)
    {
        var client = _graphFactory.CreateOnBehalfOf(bearerToken);

        try
        {
            var existing = await client.Subscriptions.GetAsync(cancellationToken: ct);
            if (existing?.Value is null) return;

            foreach (var sub in existing.Value)
            {
                if (sub.Id is null) continue;
                try
                {
                    await client.Subscriptions[sub.Id].DeleteAsync(cancellationToken: ct);
                    _logger.LogInformation("Deleted Graph subscription {SubscriptionId}", sub.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete subscription {SubscriptionId}", sub.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list subscriptions for deletion");
            throw new GraphException("Failed to enumerate subscriptions for deletion");
        }
    }
}
