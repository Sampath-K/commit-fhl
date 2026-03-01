using CommitApi.Entities;
using CommitApi.Models.Extraction;

namespace CommitApi.Services;

/// <summary>
/// Assigns an Eisenhower quadrant priority to a raw commitment.
/// </summary>
public interface IEisenhowerScorer
{
    /// <summary>
    /// Computes the priority for <paramref name="commitment"/>.
    /// </summary>
    /// <returns>One of: urgent-important, schedule, delegate, defer.</returns>
    string Score(RawCommitment commitment);
}
