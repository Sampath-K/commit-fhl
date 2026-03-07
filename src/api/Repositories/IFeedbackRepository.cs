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
}
