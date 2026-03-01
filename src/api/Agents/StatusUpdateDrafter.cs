using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Models.Agents;
using CommitApi.Models.Graph;

namespace CommitApi.Agents;

/// <summary>
/// Given a chosen replan option (Option C — clean slip + auto-comms),
/// generates personalised Teams message drafts for each watcher.
/// Each watcher gets a scoped message based on their relationship to the affected task.
/// </summary>
public interface IStatusUpdateDrafter
{
    /// <summary>
    /// Produces one <see cref="AgentDraft"/> per watcher in the cascade.
    /// </summary>
    IReadOnlyList<AgentDraft> DraftUpdates(
        CommitmentEntity    root,
        ReplanOption        chosenOption,
        IEnumerable<string> watcherUserIds);
}

public sealed class StatusUpdateDrafter : IStatusUpdateDrafter
{
    private readonly ILogger<StatusUpdateDrafter> _log;

    public StatusUpdateDrafter(ILogger<StatusUpdateDrafter> log) => _log = log;

    /// <inheritdoc />
    public IReadOnlyList<AgentDraft> DraftUpdates(
        CommitmentEntity    root,
        ReplanOption        chosenOption,
        IEnumerable<string> watcherUserIds)
    {
        var drafts = new List<AgentDraft>();

        foreach (var watcherId in watcherUserIds)
        {
            var content = BuildMessageForWatcher(root, chosenOption, watcherId);

            drafts.Add(new AgentDraft(
                DraftId:        Guid.NewGuid().ToString(),
                ActionType:     "send-message",
                Content:        content,
                ContextSummary: $"Re: {PiiScrubber.HashValue(root.Title)} — {chosenOption.Label} chosen",
                Recipients:     [watcherId],
                CreatedAt:      DateTimeOffset.UtcNow,
                Status:         "pending",
                EditedContent:  null));
        }

        _log.LogInformation("StatusUpdateDrafter: generated {Count} draft(s) for replan option {OptionId}",
            drafts.Count, chosenOption.OptionId);

        return drafts;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static string BuildMessageForWatcher(
        CommitmentEntity root,
        ReplanOption     option,
        string           watcherId)
    {
        // In a real implementation this would use Azure OpenAI to personalise.
        // For the demo, we use template-based drafts scoped by option.
        var slipNote = option.OptionId switch
        {
            "A" => "I'm fast-tracking this — expect it resolved by end of day.",
            "B" => "I'm running this in parallel with other work — ETA slightly extended but controlled.",
            "C" => $"There's a planned slip on this item. {option.Description}",
            _   => option.Description,
        };

        return $"Hi — heads up on a commitment you're watching.\n\n"
             + $"{slipNote}\n\n"
             + $"Required next steps:\n"
             + string.Join("\n", option.RequiredActions.Take(3).Select(a => $"• {a}"))
             + "\n\nNo action needed from you — just wanted to keep you informed.";
    }
}
