using System.Diagnostics;
using System.Text.Json;
using CommitApi.Entities;
using CommitApi.Exceptions;
using CommitApi.Models.Graph;
using CommitApi.Repositories;

namespace CommitApi.Graph;

/// <summary>
/// BFS-based cascade simulator.  Starting from a root task that has slipped, propagates
/// the delay through the dependency graph (following BlocksJson edges).
///
/// Calendar pressure is estimated from existing commitment density around the projected ETA
/// (number of pending commitments due within ±1 day / 5 expected max per day).
/// </summary>
public class CascadeSimulator : ICascadeSimulator
{
    private const double HighPressureThreshold = 0.80;
    private const double PressureSlipFactor    = 1.0;   // extra days added when pressure > threshold

    private readonly ICommitmentRepository _repo;
    private readonly ILogger<CascadeSimulator> _log;

    public CascadeSimulator(ICommitmentRepository repo, ILogger<CascadeSimulator> log)
    {
        _repo = repo;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<CascadeResult> SimulateAsync(
        string rootTaskId, string userId, int slipDays, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Pre-fetch all user commitments once (used for calendar pressure)
        var allCommitments = await _repo.ListByOwnerAsync(userId, ct: ct);
        var byId           = allCommitments.ToDictionary(e => e.RowKey, StringComparer.OrdinalIgnoreCase);

        if (!byId.TryGetValue(rootTaskId, out var rootEntity))
            throw new NotFoundException($"Commitment '{rootTaskId}' not found for user.");

        // BFS
        var visited = new Dictionary<string, AffectedTask>(StringComparer.OrdinalIgnoreCase);
        var queue   = new Queue<(string taskId, int cumulativeSlip)>();
        queue.Enqueue((rootTaskId, slipDays));

        while (queue.Count > 0)
        {
            var (taskId, cumSlip) = queue.Dequeue();
            if (visited.ContainsKey(taskId))
                continue;

            if (!byId.TryGetValue(taskId, out var entity))
                continue;  // cross-user or deleted — skip

            var originalEta = entity.DueAt;
            var newEta      = originalEta.HasValue
                ? originalEta.Value.AddDays(cumSlip)
                : (DateTimeOffset?)null;

            var pressure = CalendarPressure(allCommitments, newEta);
            var pressureExtra = pressure > HighPressureThreshold
                ? (int)Math.Ceiling(pressure * PressureSlipFactor)
                : 0;

            visited[taskId] = new AffectedTask(
                TaskId:           taskId,
                Title:            entity.Title,
                CumulativeSlipDays: cumSlip,
                OriginalEta:      originalEta,
                NewEta:           newEta,
                CalendarPressure: pressure);

            // Traverse downstream dependents (what this task blocks)
            var blocks = DeserializeIds(entity.BlocksJson);
            foreach (var blockedId in blocks)
            {
                if (visited.ContainsKey(blockedId))
                    continue;

                if (!byId.TryGetValue(blockedId, out var blockedEntity))
                    continue;

                if (newEta.HasValue && blockedEntity.DueAt.HasValue &&
                    newEta.Value > blockedEntity.DueAt.Value)
                {
                    var additionalSlip = (int)Math.Ceiling(
                        (newEta.Value - blockedEntity.DueAt.Value).TotalDays) + pressureExtra;

                    queue.Enqueue((blockedId, additionalSlip));
                }
            }
        }

        var affectedList = visited.Values.ToList();
        var totalPressure = affectedList.Sum(t => t.CalendarPressure);

        _log.LogInformation(
            "Cascade from {Root}: {Count} tasks affected, total pressure {Pressure:F2}, elapsed {Ms}ms (target <10000ms)",
            rootTaskId, affectedList.Count, totalPressure, sw.ElapsedMilliseconds);

        return new CascadeResult(rootTaskId, slipDays, affectedList, totalPressure);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Estimates calendar pressure around a projected ETA by counting pending commitments
    /// due within ±1 day and dividing by an assumed max of 5 tasks/day.
    /// </summary>
    private static double CalendarPressure(
        IReadOnlyList<CommitmentEntity> all, DateTimeOffset? newEta)
    {
        if (!newEta.HasValue)
            return 0.0;

        var window = TimeSpan.FromDays(1);
        var count  = all.Count(e =>
            e.Status != "done" &&
            e.DueAt.HasValue &&
            Math.Abs((e.DueAt.Value - newEta.Value).TotalDays) <= window.TotalDays);

        return Math.Min(1.0, count / 5.0);
    }

    private static string[] DeserializeIds(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}
