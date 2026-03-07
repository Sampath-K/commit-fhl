using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;

namespace CommitApi.Repositories;

/// <summary>
/// Azure Table Storage implementation of <see cref="IFeedbackRepository"/>.
/// Table: feedback. PK = SHA-256(userId), RK = Guid.
/// </summary>
public sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly TableClient _table;

    public FeedbackRepository(TableClient tableClient)
    {
        _table = tableClient;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(FeedbackEntity entity, CancellationToken ct = default)
    {
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeedbackEntity>> GetByUserHashAsync(string userHash, CancellationToken ct = default)
    {
        var filter  = $"PartitionKey eq '{userHash}'";
        var results = new List<FeedbackEntity>();
        await foreach (var e in _table.QueryAsync<FeedbackEntity>(filter, cancellationToken: ct))
            results.Add(e);
        return results.OrderBy(e => e.RecordedAt).ToList();
    }

    /// <inheritdoc/>
    public async Task DeleteAllForUserAsync(string userHash, CancellationToken ct = default)
    {
        var all = await GetByUserHashAsync(userHash, ct);
        const int batchSize = 100;
        var batches = all
            .Select((e, i) => (e, i))
            .GroupBy(x => x.i / batchSize)
            .Select(g => g.Select(x => x.e).ToList());

        foreach (var batch in batches)
        {
            var actions = batch.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e));
            await _table.SubmitTransactionAsync(actions, ct);
        }
    }
}
