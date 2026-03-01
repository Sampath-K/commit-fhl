using CommitApi.Models.Extraction;

namespace CommitApi.Services;

/// <summary>
/// Assigns an Eisenhower quadrant to a commitment based on urgency and importance signals.
/// </summary>
/// <remarks>
/// Urgency:   due &lt; 48 hrs  OR  watchers are actively pinging
/// Importance: blocks others  OR  has exec-level watchers (heuristic: watcher count &gt; 2)
/// </remarks>
public sealed class EisenhowerScorer : IEisenhowerScorer
{
    /// <inheritdoc/>
    public string Score(RawCommitment commitment)
    {
        var urgent    = IsUrgent(commitment);
        var important = IsImportant(commitment);

        return (urgent, important) switch
        {
            (true,  true)  => "urgent-important",   // Do first — crises, tight deadlines with broad impact
            (false, true)  => "schedule",            // Plan — strategic work, no burning deadline
            (true,  false) => "delegate",            // Delegate — low-impact but time-pressured
            (false, false) => "defer",               // Eliminate or defer — low urgency, low importance
        };
    }

    private static bool IsUrgent(RawCommitment c)
    {
        if (!c.DueAt.HasValue) return false;
        var hoursUntilDue = (c.DueAt.Value - DateTimeOffset.UtcNow).TotalHours;
        return hoursUntilDue <= 48;
    }

    private static bool IsImportant(RawCommitment c)
    {
        // Exec visibility heuristic: many watchers implies high organisational importance
        if (c.WatcherUserIds.Length >= 2) return true;

        // High NLP confidence on transcripts = likely explicitly stated commitment with audience
        if (c.SourceType == CommitmentSourceType.Transcript && c.Confidence >= 0.85) return true;

        // Unresolved PR review = blocks the PR author from shipping
        if (c.SourceType == CommitmentSourceType.Ado) return true;

        return false;
    }
}
