using CommitApi.Models.Extraction;

namespace CommitApi.Services;

/// <summary>
/// Merges commitments that refer to the same underlying task to prevent duplicates.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Groups <paramref name="rawCommitments"/> by similarity and returns one
    /// merged <see cref="RawCommitment"/> per logical task.
    /// Idempotent: running twice on the same input produces the same output.
    /// </summary>
    IReadOnlyList<RawCommitment> Deduplicate(IEnumerable<RawCommitment> rawCommitments);
}
