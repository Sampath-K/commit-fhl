using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Reads active Microsoft Planner tasks (including Loop tasks, which sync to Planner)
/// assigned to the current user and surfaces them as commitments.
/// </summary>
public interface IPlannerExtractor
{
    /// <summary>
    /// Returns raw commitments derived from active Planner tasks created within the past
    /// <paramref name="days"/> days that have a non-null due date.
    /// </summary>
    /// <param name="bearerToken">User bearer token for OBO Graph call.</param>
    /// <param name="days">Creation look-back window in days (default 7).</param>
    Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default);
}
