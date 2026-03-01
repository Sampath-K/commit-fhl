using CommitApi.Models.Graph;

namespace CommitApi.Graph;

/// <summary>
/// Detects dependency edges between a user's commitments using three signals:
/// shared conversation thread, overlapping people, and NLP title similarity.
/// </summary>
public interface IDependencyLinker
{
    /// <summary>
    /// Builds (or refreshes) the dependency graph for a user.
    /// Detected edges are persisted by updating BlockedByJson / BlocksJson on each
    /// CommitmentEntity, and returned as a list of GraphEdge records.
    /// </summary>
    /// <param name="userId">AAD Object ID of the user whose commitments to analyse.</param>
    /// <param name="bearerToken">OBO token for Graph API calls (not used by default linker,
    /// but required for future GPT-embedding signal).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All detected GraphEdge records (may be empty if no dependencies found).</returns>
    Task<IReadOnlyList<GraphEdge>> BuildGraphAsync(
        string userId,
        string bearerToken,
        CancellationToken ct = default);
}
