using CommitApi.Models.Extraction;

namespace CommitApi.Services;

/// <summary>
/// Runs extracted text through Azure OpenAI to identify and score commitments.
/// </summary>
public interface INlpPipeline
{
    /// <summary>
    /// Analyses a list of transcript chunks and returns refined commitments.
    /// Low-confidence results (below 0.6) are filtered out.
    /// </summary>
    Task<IReadOnlyList<RawCommitment>> ExtractFromChunksAsync(
        IEnumerable<TranscriptChunk> chunks,
        CancellationToken ct = default);

    /// <summary>
    /// Re-scores a heuristic commitment from chat/email/ADO with higher-quality NLP.
    /// </summary>
    Task<RawCommitment?> RefineAsync(
        RawCommitment heuristic,
        CancellationToken ct = default);
}
