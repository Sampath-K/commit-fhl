using CommitApi.Entities;

namespace CommitApi.Repositories;

/// <summary>Persists and queries user feedback on extracted commitments.</summary>
public interface IFeedbackRepository
{
    /// <summary>Records a new feedback event (PII-scrubbed before storage).</summary>
    Task RecordAsync(FeedbackEntity entity, CancellationToken ct = default);

    /// <summary>Returns all feedback for the given user hash in chronological order.</summary>
    Task<IReadOnlyList<FeedbackEntity>> GetByUserHashAsync(string userHash, CancellationToken ct = default);

    /// <summary>
    /// Deletes all feedback rows for a user (GDPR right-to-erasure).
    /// Called by DELETE /api/v1/users/{userId}/data.
    /// </summary>
    Task DeleteAllForUserAsync(string userHash, CancellationToken ct = default);

    /// <summary>
    /// Scans up to <paramref name="limit"/> feedback rows across all users for admin KPIs.
    /// Returns total count, false-positive count, and average confidence at feedback time.
    /// </summary>
    Task<(int Total, int FalsePositives, double AvgConfidence)> GetAdminStatsAsync(
        int limit = 2000, CancellationToken ct = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> recent feedback rows across all users,
    /// optionally filtered by type and/or source. Sorted newest-first.
    /// </summary>
    Task<IReadOnlyList<FeedbackEntity>> GetRecentAsync(
        string? typeFilter   = null,
        string? sourceFilter = null,
        int     limit        = 200,
        CancellationToken ct = default);
}
