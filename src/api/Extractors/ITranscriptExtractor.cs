using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Fetches Teams meeting transcripts for the authenticated user
/// and splits them into per-speaker chunks.
/// </summary>
public interface ITranscriptExtractor
{
    /// <summary>
    /// Returns all speaker chunks from meetings in the past <paramref name="days"/> days.
    /// </summary>
    /// <param name="bearerToken">User bearer token for OBO Graph call.</param>
    /// <param name="days">Look-back window in days (default 7).</param>
    Task<IReadOnlyList<TranscriptChunk>> GetChunksAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default);
}
