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
    public async Task<(int Total, int FalsePositives, double AvgConfidence)> GetAdminStatsAsync(
        int limit = 2000, CancellationToken ct = default)
    {
        var rows = new List<FeedbackEntity>();
        await foreach (var e in _table.QueryAsync<FeedbackEntity>(cancellationToken: ct))
        {
            rows.Add(e);
            if (rows.Count >= limit) break;
        }
        int total          = rows.Count;
        int falsePositives = rows.Count(r => r.FeedbackType == "FalsePositive");
        double avgConf     = total > 0 ? rows.Average(r => r.ConfidenceAtFeedback) : 0.0;
        return (total, falsePositives, avgConf);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FeedbackEntity>> GetRecentAsync(
        string? typeFilter   = null,
        string? sourceFilter = null,
        int     limit        = 200,
        CancellationToken ct = default)
    {
        // Build OData filter — cross-partition scan, client-side sort
        var filters = new List<string>();
        if (!string.IsNullOrEmpty(typeFilter))   filters.Add($"FeedbackType eq '{typeFilter}'");
        if (!string.IsNullOrEmpty(sourceFilter)) filters.Add($"SourceType eq '{sourceFilter}'");
        var odata  = filters.Count > 0 ? string.Join(" and ", filters) : null;

        var rows = new List<FeedbackEntity>();
        await foreach (var e in _table.QueryAsync<FeedbackEntity>(odata, cancellationToken: ct))
        {
            rows.Add(e);
            if (rows.Count >= limit * 3) break; // over-fetch before sort+trim
        }

        return rows
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .ToList();
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
