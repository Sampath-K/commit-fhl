namespace CommitApi.Repositories;

/// <summary>
/// Stores per-user, per-extractor sync state to enable delta/incremental extraction.
/// </summary>
public interface ISyncStateRepository
{
    /// <summary>Returns the saved Graph delta token, or null if no previous run exists.</summary>
    Task<string?> GetDeltaTokenAsync(string userId, string extractor, CancellationToken ct = default);

    /// <summary>Saves or overwrites the Graph delta token for the given user+extractor.</summary>
    Task SaveDeltaTokenAsync(string userId, string extractor, string deltaToken, CancellationToken ct = default);

    /// <summary>Returns the saved watermark timestamp, or null if no previous run exists.</summary>
    Task<DateTimeOffset?> GetWatermarkAsync(string userId, string extractor, CancellationToken ct = default);

    /// <summary>Saves or overwrites the watermark for the given user+extractor.</summary>
    Task SaveWatermarkAsync(string userId, string extractor, DateTimeOffset watermark, CancellationToken ct = default);
}
