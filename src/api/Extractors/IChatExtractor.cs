using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Reads Teams DM and channel messages to surface commitment signals.
/// </summary>
public interface IChatExtractor
{
    /// <summary>
    /// Returns raw commitments detected in Teams chats from the past <paramref name="days"/> days.
    /// Only messages with action-intent signals (will, I'll, by Friday, etc.) are included.
    /// </summary>
    /// <param name="bearerToken">User bearer token for OBO Graph call.</param>
    /// <param name="days">Look-back window in days (default 3).</param>
    Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 3,
        CancellationToken ct = default);
}
