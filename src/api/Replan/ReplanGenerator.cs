using CommitApi.Config;
using CommitApi.Models.Graph;
using CommitApi.Repositories;

namespace CommitApi.Replan;

/// <summary>
/// Generates three distinct replan strategies for a given cascade:
///
///   Option A — Resolve Fast:
///     Pull in extra resources or simplify scope on the root task.
///     Highest risk if feasibility is unknown; highest confidence when team has slack.
///
///   Option B — Parallel Work:
///     Start dependent tasks in parallel with the still-in-progress root.
///     Moderate risk (rework possible); good when dependencies are partial.
///
///   Option C — Clean Slip + Auto-Comms:
///     Accept the delay, update ETAs, draft status messages to all watchers.
///     Lowest risk; preserves trust by communicating proactively.
/// </summary>
public class ReplanGenerator : IReplanGenerator
{
    private readonly ICommitmentRepository _repo;
    private readonly ILogger<ReplanGenerator> _log;

    public ReplanGenerator(ICommitmentRepository repo, ILogger<ReplanGenerator> log)
    {
        _repo = repo;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReplanOption>> GenerateAsync(
        CascadeResult cascade, string userId, CancellationToken ct = default)
    {
        var root = await _repo.GetAsync(userId, cascade.RootTaskId, ct);
        var affectedCount = cascade.TotalTasksAffected;
        var slipDays      = cascade.InputSlipDays;

        // Confidence heuristics based on cascade severity
        var aConfidence = slipDays <= 2 ? 0.75 : 0.50;   // resolve fast: harder if big slip
        var bConfidence = 0.65;                            // parallel: consistent moderate confidence
        var cConfidence = 0.90 - (slipDays * 0.02);       // clean slip: very reliable, degrades slightly

        var rootTitle = root?.Title ?? cascade.RootTaskId;
        var peopleAffected = cascade.AffectedTasks
            .Select(t => t.TaskId)
            .Distinct()
            .Count();

        _log.LogInformation(
            "Replan options generated for {Root} (user={Hash}): {Count} affected",
            cascade.RootTaskId, PiiScrubber.HashValue(userId), affectedCount);

        return
        [
            new ReplanOption(
                OptionId: "A",
                Label:    "Resolve Fast",
                Description: $"Expedite '{rootTitle}' by adding resources or reducing scope. " +
                             $"Best if you have team capacity — removes the {slipDays}-day slip entirely.",
                Confidence:  Math.Round(aConfidence, 2),
                RequiredActions:
                [
                    $"Identify scope that can be deferred from '{rootTitle}'",
                    "Pull in one team member to pair on the critical path",
                    "Update ETA in tracking system to original date",
                    $"Notify {peopleAffected} downstream owners that slip is resolved"
                ]),

            new ReplanOption(
                OptionId: "B",
                Label:    "Parallel Work",
                Description: $"Start {affectedCount - 1} downstream task(s) in parallel while " +
                             $"'{rootTitle}' finishes. Reduces end-to-end delay at risk of some rework.",
                Confidence:  Math.Round(bConfidence, 2),
                RequiredActions:
                [
                    $"Begin work on dependent tasks that have partial unblocking",
                    "Identify which downstream deliverables are independent of the blocked interface",
                    $"Set a sync checkpoint in {slipDays} days to merge parallel work",
                    "Accept rework risk on parallel track if upstream changes interface"
                ]),

            new ReplanOption(
                OptionId: "C",
                Label:    "Clean Slip + Auto-Comms",
                Description: $"Accept the {slipDays}-day slip, update all ETAs, and send proactive " +
                             $"status messages to {peopleAffected} watcher(s). Lowest risk to quality.",
                Confidence:  Math.Round(Math.Max(0.60, cConfidence), 2),
                RequiredActions:
                [
                    $"Update root task ETA by {slipDays} day(s)",
                    $"Propagate new ETAs to {affectedCount - 1} downstream task(s)",
                    "Draft and send status update to each watcher (agent will prepare drafts)",
                    "Log slip reason for retrospective — this prevents recurrence patterns"
                ])
        ];
    }
}
