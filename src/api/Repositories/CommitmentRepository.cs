using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;
using CommitApi.Exceptions;

namespace CommitApi.Repositories;

/// <summary>
/// Azure Table Storage implementation of ICommitmentRepository.
/// This is the ONLY class that calls TableClient directly (P-20).
/// </summary>
public sealed class CommitmentRepository : ICommitmentRepository
{
    private readonly TableClient _tableClient;

    private const string TableName = "commitments";

    /// <summary>
    /// Initializes the repository with an Azure Table Storage client.
    /// </summary>
    /// <param name="tableClient">Pre-configured TableClient pointing to the commitments table.</param>
    public CommitmentRepository(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(CommitmentEntity entity, CancellationToken ct = default)
    {
        try
        {
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
        }
        catch (RequestFailedException ex)
        {
            throw new StorageException($"Failed to upsert commitment {entity.RowKey}", TableName)
            {
                // Preserve original exception context for logging
                Source = ex.Source
            };
        }
    }

    /// <inheritdoc />
    public async Task<CommitmentEntity?> GetAsync(string userId, string rowKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<CommitmentEntity>(userId, rowKey, cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException ex)
        {
            throw new StorageException($"Failed to get commitment {rowKey} for user {userId}", TableName)
            {
                Source = ex.Source
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommitmentEntity>> ListByOwnerAsync(string userId,
        DateTimeOffset? since = null, CancellationToken ct = default)
    {
        try
        {
            var filter = since.HasValue
                ? $"PartitionKey eq '{userId}' and CommittedAt ge datetime'{since.Value:O}'"
                : $"PartitionKey eq '{userId}'";

            var results = new List<CommitmentEntity>();
            await foreach (var entity in _tableClient.QueryAsync<CommitmentEntity>(filter, cancellationToken: ct))
            {
                results.Add(entity);
            }
            return results;
        }
        catch (RequestFailedException ex)
        {
            throw new StorageException($"Failed to list commitments for user {userId}", TableName)
            {
                Source = ex.Source
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommitmentEntity>> ListBlockingAsync(string userId,
        string blockedCommitmentId, CancellationToken ct = default)
    {
        // Fetch all for user then filter in memory — OData doesn't support JSON array contains.
        // Acceptable for pilot scale (P-01): max ~200 commitments per user.
        var all = await ListByOwnerAsync(userId, since: null, ct);

        return all
            .Where(e =>
            {
                var blocks = DeserializeIds(e.BlocksJson);
                return blocks.Contains(blockedCommitmentId);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string userId, string rowKey, CancellationToken ct = default)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(userId, rowKey, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Idempotent — deleting a non-existent entity is not an error.
        }
        catch (RequestFailedException ex)
        {
            throw new StorageException($"Failed to delete commitment {rowKey}", TableName)
            {
                Source = ex.Source
            };
        }
    }

    /// <inheritdoc />
    public async Task DeleteAllForUserAsync(string userId, CancellationToken ct = default)
    {
        // Fetch all entities then batch-delete (Table Storage max batch size = 100).
        var all = await ListByOwnerAsync(userId, since: null, ct);

        const int batchSize = 100;
        var batches = all
            .Select((e, i) => (e, i))
            .GroupBy(x => x.i / batchSize)
            .Select(g => g.Select(x => x.e).ToList());

        foreach (var batch in batches)
        {
            var actions = batch.Select(e =>
                new TableTransactionAction(TableTransactionActionType.Delete, e));
            try
            {
                await _tableClient.SubmitTransactionAsync(actions, ct);
            }
            catch (RequestFailedException ex)
            {
                throw new StorageException($"Failed to batch-delete commitments for user {userId}", TableName)
                {
                    Source = ex.Source
                };
            }
        }
    }

    /// <inheritdoc/>
    public async Task<int> CountAllAsync(int limit = 5000, CancellationToken ct = default)
    {
        int count = 0;
        await foreach (var _ in _tableClient.QueryAsync<CommitmentEntity>(
            select: ["RowKey"], cancellationToken: ct))
        {
            if (++count >= limit) break;
        }
        return count;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> DeserializeIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
}
