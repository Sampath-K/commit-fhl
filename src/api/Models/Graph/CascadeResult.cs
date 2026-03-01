namespace CommitApi.Models.Graph;

/// <summary>
/// A single commitment affected by the cascade, with its new projected ETA.
/// </summary>
/// <param name="TaskId">Commitment row key.</param>
/// <param name="Title">Commitment title (never put in telemetry — P-12).</param>
/// <param name="CumulativeSlipDays">Days this task is projected to slip.</param>
/// <param name="OriginalEta">Original due date before the cascade.</param>
/// <param name="NewEta">Projected new due date after slip propagation.</param>
/// <param name="CalendarPressure">0–1 fraction of owner's calendar blocked around new ETA.</param>
public record AffectedTask(
    string TaskId,
    string Title,
    int CumulativeSlipDays,
    DateTimeOffset? OriginalEta,
    DateTimeOffset? NewEta,
    double CalendarPressure);

/// <summary>
/// Output of a cascade simulation run.
/// </summary>
/// <param name="RootTaskId">The task whose slip triggered the cascade.</param>
/// <param name="InputSlipDays">Days of slip applied to the root task.</param>
/// <param name="AffectedTasks">All tasks impacted, including the root (BFS order).</param>
/// <param name="TotalCalendarPressure">Sum of calendar pressure across all affected tasks.</param>
public record CascadeResult(
    string RootTaskId,
    int InputSlipDays,
    IReadOnlyList<AffectedTask> AffectedTasks,
    double TotalCalendarPressure)
{
    /// <summary>Number of tasks affected (including root).</summary>
    public int TotalTasksAffected => AffectedTasks.Count;
}
