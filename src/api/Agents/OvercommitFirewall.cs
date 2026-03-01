using CommitApi.Capacity;
using CommitApi.Models.Agents;

namespace CommitApi.Agents;

/// <summary>
/// Intercepts when a user's load index exceeds 90% and generates a warning
/// Adaptive Card draft instead of silently accepting the new commitment.
/// </summary>
public interface IOvercommitFirewall
{
    /// <summary>
    /// Returns a warning <see cref="AgentDraft"/> if load is above the threshold,
    /// or <c>null</c> if the user has capacity.
    /// </summary>
    Task<AgentDraft?> CheckAsync(string userId, string bearerToken, CancellationToken ct = default);
}

public sealed class OvercommitFirewall : IOvercommitFirewall
{
    private const double LoadThreshold = 0.90;

    private readonly IVivaInsightsClient            _viva;
    private readonly ILogger<OvercommitFirewall>    _log;

    public OvercommitFirewall(IVivaInsightsClient viva, ILogger<OvercommitFirewall> log)
    {
        _viva = viva;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<AgentDraft?> CheckAsync(string userId, string bearerToken, CancellationToken ct = default)
    {
        var snapshot = await _viva.GetCapacityAsync(userId, bearerToken, ct);

        if (snapshot.LoadIndex < LoadThreshold)
            return null; // User has capacity — no warning needed

        _log.LogInformation("OvercommitFirewall: user {UserId} at load {Load:P0} — generating warning",
            userId, snapshot.LoadIndex);

        var nextSlot = snapshot.FreeSlots.FirstOrDefault();
        var slotNote = nextSlot is not null
            ? $"Your next free 2-hour block is {nextSlot.Start:ddd h:mm tt} – {nextSlot.End:h:mm tt}."
            : "No free slots found in the next 3 days.";

        var content =
            $"⚠️ You're currently at {snapshot.LoadIndex:P0} capacity.\n\n"
          + $"{slotNote}\n\n"
          + "Taking on additional work now risks your current commitments. "
          + "Consider deferring, delegating, or negotiating scope.";

        return new AgentDraft(
            DraftId:        Guid.NewGuid().ToString(),
            ActionType:     "send-message",
            Content:        content,
            ContextSummary: $"Load index {snapshot.LoadIndex:P0} — overcommit warning",
            Recipients:     [userId],
            CreatedAt:      DateTimeOffset.UtcNow,
            Status:         "pending",
            EditedContent:  null);
    }
}
