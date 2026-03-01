using CommitApi.Entities;
using CommitApi.Models.Graph;

namespace CommitApi.Graph;

/// <summary>
/// Scores the business impact of a cascade using the formula:
///   (people × 10) + (calendarHrs × 5) + (execVisibility × 20) + (daysToDateDep × -2)
/// clamped to [0, 100].
///
/// Component definitions:
///   people         — distinct watcher count across all affected commitments
///   calendarHrs    — count of affected tasks × 2 hrs (heuristic per commitment)
///   execVisibility — number of affected tasks with Priority = "urgent-important" OR ≥ 3 watchers
///   daysToDateDep  — days until the earliest affected task's original due date (0 if past due)
/// </summary>
public class ImpactScorer : IImpactScorer
{
    private const int PeopleWeight      = 10;
    private const int CalHrsWeight      = 5;
    private const int ExecWeight        = 20;
    private const int DateDepWeight     = -2;
    private const int HoursPerTask      = 2;   // heuristic: each commitment ~2 hrs effort

    /// <inheritdoc />
    public int Score(CascadeResult cascade, IEnumerable<CommitmentEntity> affectedEntities)
    {
        var entities = affectedEntities.ToList();
        if (entities.Count == 0)
            return 0;

        // People: distinct watcher AAD OIDs
        var allWatchers = entities
            .SelectMany(e => DeserializeWatchers(e.WatchersJson))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        // Calendar hours: heuristic 2 hrs per affected commitment
        var calHrs = entities.Count * HoursPerTask;

        // Exec visibility: tasks with exec exposure heuristic
        var execVisible = entities.Count(e =>
            e.Priority == "urgent-important" ||
            DeserializeWatchers(e.WatchersJson).Length >= 3);

        // Days to earliest date dependency (non-negative)
        var now = DateTimeOffset.UtcNow;
        var daysToDateDep = entities
            .Where(e => e.DueAt.HasValue && e.DueAt.Value > now)
            .Select(e => (e.DueAt!.Value - now).TotalDays)
            .DefaultIfEmpty(0.0)
            .Min();
        var daysInt = (int)Math.Max(0, Math.Floor(daysToDateDep));

        var rawScore =
            (allWatchers * PeopleWeight) +
            (calHrs      * CalHrsWeight) +
            (execVisible * ExecWeight)   +
            (daysInt     * DateDepWeight);

        return Math.Clamp(rawScore, 0, 100);
    }

    private static string[] DeserializeWatchers(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
