using CommitApi.Models.Capacity;

namespace CommitApi.Capacity;

/// <summary>
/// Retrieves Viva Insights activity statistics and free-slot data for a user.
/// Falls back to calendar-derived estimates when Viva Insights is not licensed.
/// </summary>
public interface IVivaInsightsClient
{
    /// <summary>
    /// Returns a capacity snapshot containing load index, burnout trend, and free slots.
    /// </summary>
    /// <param name="userId">AAD Object ID (for telemetry hashing — not sent to Graph).</param>
    /// <param name="bearerToken">OBO bearer token for Graph API calls.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>CapacitySnapshot with loadIndex, burnoutTrend, and freeSlots.</returns>
    Task<CapacitySnapshot> GetCapacityAsync(
        string userId,
        string bearerToken,
        CancellationToken ct = default);
}
