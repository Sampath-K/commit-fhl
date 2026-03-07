using Azure;
using Azure.Data.Tables;

namespace CommitApi.Entities;

/// <summary>
/// Persists per-user, per-extractor sync state (delta tokens + watermarks) in Azure Table Storage.
/// Table: syncstate, PK = userId, RK = extractorName
/// </summary>
public sealed class SyncStateEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey       { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>Graph @odata.deltaLink for delta-query extractors (Email, Planner, Drive).</summary>
    public string? DeltaToken { get; set; }

    /// <summary>Watermark timestamp for polling extractors (Chat, Transcript, ADO).</summary>
    public DateTimeOffset? Watermark { get; set; }
}
