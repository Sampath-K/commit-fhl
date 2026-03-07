using CommitApi.Models.Extraction;
using CommitApi.Models.Feedback;

namespace CommitApi.Services;

/// <summary>
/// Assigns an Eisenhower quadrant priority to a raw commitment.
/// </summary>
public interface IEisenhowerScorer
{
    /// <summary>
    /// Computes the priority for <paramref name="commitment"/>.
    /// The optional <paramref name="profile"/> adjusts the transcript confidence threshold.
    /// </summary>
    /// <returns>One of: urgent-important, schedule, delegate, defer.</returns>
    string Score(RawCommitment commitment, UserSignalProfile? profile = null);
}
