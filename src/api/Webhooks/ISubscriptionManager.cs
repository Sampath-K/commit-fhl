namespace CommitApi.Webhooks;

/// <summary>
/// Manages Microsoft Graph change notification subscriptions.
/// Subscriptions allow the API to receive real-time push notifications
/// when Teams messages or Outlook emails arrive.
/// </summary>
public interface ISubscriptionManager
{
    /// <summary>
    /// Registers (or renews) Graph change notification subscriptions for the given user.
    /// Subscribes to: /me/chats/getAllMessages and /me/mailFolders/inbox/messages.
    /// </summary>
    /// <param name="bearerToken">OBO token for the user to subscribe on behalf of.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of active subscription IDs.</returns>
    Task<IReadOnlyList<string>> EnsureSubscriptionsAsync(string bearerToken,
        CancellationToken ct = default);

    /// <summary>
    /// Removes all Graph change notification subscriptions for the given user.
    /// Called during right-to-erasure (T-C06).
    /// </summary>
    Task DeleteAllSubscriptionsAsync(string bearerToken, CancellationToken ct = default);
}
