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

    /// <summary>
    /// Batch-classifies whether each commitment has been completed based on follow-up
    /// messages from the same person. Returns one result per commitment, same order.
    /// One LLM call covers all commitments — designed for cost efficiency.
    /// </summary>
    Task<IReadOnlyList<ResolutionClassification>> ClassifyResolutionAsync(
        IReadOnlyList<string> commitmentTitles,
        IReadOnlyList<string[]> followUpMessages,
        CancellationToken ct = default);
}

/// <summary>GPT classification result for one commitment.</summary>
public sealed record ResolutionClassification(
    bool   Resolved,
    double Confidence,
    string Evidence);
