using CommitApi.Entities;
using CommitApi.Models.Graph;

namespace CommitApi.Graph;

/// <summary>
/// Computes a 0–100 business-impact score for a cascade simulation result.
/// Formula: (people × 10) + (calendarHrs × 5) + (execVisibility × 20) + (daysToDateDep × -2),
/// clamped to [0, 100].
/// </summary>
public interface IImpactScorer
{
    /// <summary>
    /// Scores the business impact of a cascade.
    /// </summary>
    /// <param name="cascade">The simulated cascade result.</param>
    /// <param name="affectedEntities">Full entity data for all tasks in the cascade.</param>
    /// <returns>Integer impact score 0–100.</returns>
    int Score(CascadeResult cascade, IEnumerable<CommitmentEntity> affectedEntities);
}
