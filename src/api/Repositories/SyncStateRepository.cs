using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;

namespace CommitApi.Repositories;

/// <summary>
/// Azure Table Storage implementation of <see cref="ISyncStateRepository"/>.
/// Table name: syncstate. PK = userId, RK = extractorName.
/// </summary>
public sealed class SyncStateRepository : ISyncStateRepository
{
    private readonly TableClient _table;

    public SyncStateRepository(TableClient tableClient)
    {
        _table = tableClient;
    }

    /// <inheritdoc/>
    public async Task<string?> GetDeltaTokenAsync(string userId, string extractor, CancellationToken ct = default)
    {
        var entity = await GetOrNullAsync(userId, extractor, ct);
        return entity?.DeltaToken;
    }

    /// <inheritdoc/>
    public async Task SaveDeltaTokenAsync(string userId, string extractor, string deltaToken, CancellationToken ct = default)
    {
        var entity = await GetOrNullAsync(userId, extractor, ct) ?? new SyncStateEntity
        {
            PartitionKey = userId,
            RowKey       = extractor,
        };
        entity.DeltaToken = deltaToken;
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetWatermarkAsync(string userId, string extractor, CancellationToken ct = default)
    {
        var entity = await GetOrNullAsync(userId, extractor, ct);
        return entity?.Watermark;
    }

    /// <inheritdoc/>
    public async Task SaveWatermarkAsync(string userId, string extractor, DateTimeOffset watermark, CancellationToken ct = default)
    {
        var entity = await GetOrNullAsync(userId, extractor, ct) ?? new SyncStateEntity
        {
            PartitionKey = userId,
            RowKey       = extractor,
        };
        entity.Watermark = watermark;
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    private async Task<SyncStateEntity?> GetOrNullAsync(string userId, string extractor, CancellationToken ct)
    {
        try
        {
            var resp = await _table.GetEntityAsync<SyncStateEntity>(userId, extractor, cancellationToken: ct);
            return resp.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
