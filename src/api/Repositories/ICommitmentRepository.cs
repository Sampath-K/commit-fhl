using CommitApi.Entities;

namespace CommitApi.Repositories;

/// <summary>
/// Repository contract for commitment storage operations.
/// All callers use this interface — only CommitmentRepository touches TableClient directly.
/// </summary>
public interface ICommitmentRepository
{
    /// <summary>
    /// Upserts a commitment. Creates if new, updates fields if the row key already exists.
    /// </summary>
    /// <param name="entity">The commitment entity to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CommitApi.Exceptions.StorageException">Thrown if Table Storage call fails.</exception>
    Task UpsertAsync(CommitmentEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single commitment by owner ID and row key.
    /// </summary>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="rowKey">RowKey — commitment UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or null if not found.</returns>
    /// <exception cref="CommitApi.Exceptions.StorageException">Thrown on storage errors (non-404).</exception>
    Task<CommitmentEntity?> GetAsync(string userId, string rowKey, CancellationToken ct = default);

    /// <summary>
    /// Lists all commitments owned by the specified user, optionally filtered by committed-after date.
    /// </summary>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="since">Optional lower bound for CommittedAt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All matching commitment entities, unsorted.</returns>
    Task<IReadOnlyList<CommitmentEntity>> ListByOwnerAsync(string userId,
        DateTimeOffset? since = null, CancellationToken ct = default);

    /// <summary>
    /// Lists commitments that have the given commitment ID in their BlocksJson list.
    /// Used to find what depends on a given task (downstream impact).
    /// </summary>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="blockedCommitmentId">Commitment ID to look for in BlocksJson.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All commitments that list the given ID in their blocks set.</returns>
    Task<IReadOnlyList<CommitmentEntity>> ListBlockingAsync(string userId,
        string blockedCommitmentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a single commitment by owner and row key.
    /// </summary>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="rowKey">RowKey — commitment UUID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string userId, string rowKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes ALL commitments for the specified user. Used for right-to-erasure (P-05, T-C06).
    /// </summary>
    /// <param name="userId">PartitionKey — owner AAD Object ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAllForUserAsync(string userId, CancellationToken ct = default);
}
