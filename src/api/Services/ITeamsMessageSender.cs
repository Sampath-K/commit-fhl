namespace CommitApi.Services;

/// <summary>
/// Sends Teams chat messages on behalf of the signed-in user via Graph OBO.
/// Requires Chat.ReadWrite + ChatMessage.Send delegated Graph scopes (admin-consented).
/// </summary>
public interface ITeamsMessageSender
{
    /// <summary>
    /// Sends <paramref name="messageContent"/> as a Teams group chat to all named recipients.
    /// The group chat is named after <paramref name="topic"/> (truncated to 50 chars).
    /// Silently skips recipients whose OIDs cannot be resolved.
    /// </summary>
    Task SendAsync(
        string   bearerToken,
        string   senderUserId,
        string   messageContent,
        string[] recipientNames,
        string   topic,
        CancellationToken ct = default);
}
