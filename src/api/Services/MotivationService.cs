using System.Text.Json;
using CommitApi.Entities;
using CommitApi.Repositories;

namespace CommitApi.Services;

/// <summary>
/// Computes the user's motivational state (delivery score, streak, XP, level)
/// from commitment history stored in Azure Table Storage.
/// </summary>
public interface IMotivationService
{
    Task<MotivationState> GetStateAsync(string userId, CancellationToken ct = default);
}

/// <summary>Serializable motivation state returned by GET /api/v1/users/{userId}/motivation.</summary>
public sealed record MotivationState(
    string   UserId,
    int      DeliveryScore,
    int      DeliveryScorePrevious,
    int      StreakDays,
    int      TotalXp,
    int      CompetencyLevel,
    double   OnTimeRate,
    double   CascadeHealthRate,
    int      TriggersShownToday,
    string   LastStreakDate);

public sealed class MotivationService : IMotivationService
{
    private readonly ICommitmentRepository       _repo;
    private readonly ILogger<MotivationService>  _log;

    // XP thresholds to reach level N (index = level - 1)
    private static readonly int[] XpThresholds = [0, 100, 300, 600, 1000];

    public MotivationService(ICommitmentRepository repo, ILogger<MotivationService> log)
    {
        _repo = repo;
        _log  = log;
    }

    public async Task<MotivationState> GetStateAsync(string userId, CancellationToken ct = default)
    {
        var all = await _repo.ListByOwnerAsync(userId, ct: ct);

        // ── On-time rate: done items where LastActivity ≤ DueAt ──────────────
        var doneItems       = all.Where(e => e.Status == "done").ToList();
        var onTimeCount     = doneItems.Count(e => e.DueAt.HasValue && e.LastActivity.HasValue
                                                    && e.LastActivity.Value <= e.DueAt.Value);
        double onTimeRate   = doneItems.Count > 0 ? (double)onTimeCount / doneItems.Count : 0;

        // ── Cascade health: done items with impact score = 0 (no cascades) ───
        var cascadeHealthy  = doneItems.Count(e => e.ImpactScore == 0);
        double cascadeRate  = doneItems.Count > 0 ? (double)cascadeHealthy / doneItems.Count : 1;

        // ── Delivery score formula ────────────────────────────────────────────
        int deliveryScore   = Math.Clamp((int)Math.Round(onTimeRate * 50 + cascadeRate * 30 + 20), 0, 100);

        // ── Streak: consecutive days with a "done" commit ─────────────────────
        var doneDates = doneItems
            .Where(e => e.LastActivity.HasValue)
            .Select(e => e.LastActivity!.Value.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int streak        = 0;
        var today         = DateTimeOffset.UtcNow.Date;
        var streakCurrent = today;
        foreach (var date in doneDates)
        {
            if (date == streakCurrent || date == streakCurrent.AddDays(-1))
            {
                streak++;
                streakCurrent = date;
            }
            else break;
        }

        var lastStreakDate = doneDates.Count > 0 ? doneDates[0].ToString("yyyy-MM-dd") : today.ToString("yyyy-MM-dd");

        // ── XP + level ────────────────────────────────────────────────────────
        int totalXp = doneItems.Count * 5;  // 5 XP per resolved commitment (simplified)
        totalXp    += doneDates.Count > 0 ? streak * 2 : 0;  // streak bonus

        int level   = 1;
        for (int i = XpThresholds.Length - 1; i >= 0; i--)
        {
            if (totalXp >= XpThresholds[i]) { level = i + 1; break; }
        }
        level = Math.Clamp(level, 1, 5);

        // ── Previous score (simple heuristic: 7 days ago) ─────────────────────
        var sevenDaysAgo  = DateTimeOffset.UtcNow.AddDays(-7);
        var olderDone     = all.Where(e => e.Status == "done" && e.LastActivity < sevenDaysAgo).ToList();
        var prevOnTime    = olderDone.Count(e => e.DueAt.HasValue && e.LastActivity.HasValue
                                                   && e.LastActivity.Value <= e.DueAt.Value);
        double prevRate   = olderDone.Count > 0 ? (double)prevOnTime / olderDone.Count : 0;
        int prevScore     = Math.Clamp((int)Math.Round(prevRate * 50 + 0.9 * 30 + 20), 0, 100);

        _log.LogDebug("MotivationService: userId={UserId} score={Score} streak={Streak} level={Level}",
            userId, deliveryScore, streak, level);

        return new MotivationState(
            UserId:                userId,
            DeliveryScore:         deliveryScore,
            DeliveryScorePrevious: prevScore,
            StreakDays:            streak,
            TotalXp:               totalXp,
            CompetencyLevel:       level,
            OnTimeRate:            onTimeRate,
            CascadeHealthRate:     cascadeRate,
            TriggersShownToday:    0, // managed client-side via sessionStorage
            LastStreakDate:        lastStreakDate);
    }
}
