using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Reads Outlook inbox to surface commitment signals from emails.
/// </summary>
public interface IEmailExtractor
{
    /// <summary>
    /// Returns raw commitments detected in unread or flagged emails
    /// from the past <paramref name="days"/> days.
    /// </summary>
    /// <param name="bearerToken">User bearer token for OBO Graph call.</param>
    /// <param name="days">Look-back window in days (default 7).</param>
    Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default);
}
