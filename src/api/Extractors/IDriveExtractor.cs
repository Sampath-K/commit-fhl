using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Scans OneDrive/SharePoint Office documents (Word, Excel, PowerPoint) for action-intent
/// comments that indicate commitments (@mentions, due-date phrases, etc.).
/// </summary>
public interface IDriveExtractor
{
    /// <summary>
    /// Returns raw commitments detected in OneDrive Office document comments
    /// from files modified within the past <paramref name="days"/> days.
    /// </summary>
    /// <param name="userId">AAD Object ID of the current user — used to filter authored files.</param>
    /// <param name="bearerToken">User bearer token for OBO Graph call.</param>
    /// <param name="days">Look-back window in days (default 3).</param>
    Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string userId,
        string bearerToken,
        int days = 3,
        CancellationToken ct = default);
}
