using CommitApi.Models.Graph;

namespace CommitApi.Replan;

/// <summary>
/// Generates three distinct replan options for a given cascade result.
/// Options: A = resolve fast, B = parallel work, C = clean slip + auto-comms.
/// </summary>
public interface IReplanGenerator
{
    /// <summary>
    /// Produces three replan options for the cascade, each with different risk/speed trade-offs.
    /// </summary>
    /// <param name="cascade">The cascade to generate options for.</param>
    /// <param name="userId">Owner AAD Object ID (for telemetry).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exactly three ReplanOption records (A, B, C) ordered by confidence descending.</returns>
    Task<IReadOnlyList<ReplanOption>> GenerateAsync(
        CascadeResult cascade,
        string userId,
        CancellationToken ct = default);
}
