using CommitApi.Models.Graph;

namespace CommitApi.Graph;

/// <summary>
/// Simulates how a slip in one commitment propagates through its dependency chain (BFS).
/// </summary>
public interface ICascadeSimulator
{
    /// <summary>
    /// Runs BFS from the root task, propagating <paramref name="slipDays"/> through the
    /// dependency graph (following BlocksJson edges). Returns projected new ETAs and
    /// calendar pressure for every affected task.
    /// </summary>
    /// <param name="rootTaskId">RowKey of the commitment that slipped.</param>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="slipDays">How many days the root task is projected to slip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full cascade result with all affected tasks and pressure metrics.</returns>
    Task<CascadeResult> SimulateAsync(
        string rootTaskId,
        string userId,
        int slipDays,
        CancellationToken ct = default);
}
