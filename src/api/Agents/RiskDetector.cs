using CommitApi.Config;
using CommitApi.Graph;
using CommitApi.Repositories;

namespace CommitApi.Agents;

/// <summary>
/// Background service that runs on a 15-minute schedule, scans for at-risk commitments,
/// and triggers cascade simulation to keep impact scores current.
///
/// A commitment is at-risk when BOTH:
///   1. No progress signal in the last 24 hours (LastActivity is null or > 24h ago)
///   2. Due in fewer than 48 hours (DueAt ≤ now + 48h)
///
/// When at-risk tasks are found, the cascade simulator re-scores their impact and
/// updates ImpactScore on each entity.
///
/// User IDs are collected at runtime from the /extract and /health endpoints.
/// </summary>
public class RiskDetector : BackgroundService
{
    private static readonly TimeSpan PollingInterval    = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActivityThreshold  = TimeSpan.FromHours(24);
    private static readonly TimeSpan DueThreshold       = TimeSpan.FromHours(48);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskDetector> _log;

    /// <summary>
    /// Set of user AAD Object IDs seen recently.  Populated by Program.cs when extract/health
    /// endpoints are called.  Only users in this set are scanned by the detector.
    /// </summary>
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset>
        RegisteredUsers = new(StringComparer.OrdinalIgnoreCase);

    public RiskDetector(IServiceScopeFactory scopeFactory, ILogger<RiskDetector> log)
    {
        _scopeFactory = scopeFactory;
        _log          = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("RiskDetector started — polling every {Interval}",
            PollingInterval);

        using var timer = new PeriodicTimer(PollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ScanAllUsersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "RiskDetector scan failed");
            }
        }

        _log.LogInformation("RiskDetector stopped");
    }

    private async Task ScanAllUsersAsync(CancellationToken ct)
    {
        if (RegisteredUsers.IsEmpty)
            return;

        // Use scoped services (repo + cascade are singletons, but scope ensures clean DI context)
        using var scope    = _scopeFactory.CreateScope();
        var repo           = scope.ServiceProvider.GetRequiredService<ICommitmentRepository>();
        var cascade        = scope.ServiceProvider.GetRequiredService<ICascadeSimulator>();
        var impactScorer   = scope.ServiceProvider.GetRequiredService<IImpactScorer>();

        var now            = DateTimeOffset.UtcNow;
        var totalAtRisk    = 0;

        foreach (var userId in RegisteredUsers.Keys)
        {
            var commitments = await repo.ListByOwnerAsync(userId, ct: ct);
            var atRisk = commitments.Where(e =>
                e.Status != "done" &&
                e.DueAt.HasValue &&
                e.DueAt.Value <= now.Add(DueThreshold) &&
                (e.LastActivity == null || now - e.LastActivity.Value >= ActivityThreshold)
            ).ToList();

            foreach (var entity in atRisk)
            {
                try
                {
                    // Simulate a 0-day slip to refresh impact scores without changing ETAs
                    var result  = await cascade.SimulateAsync(entity.RowKey, userId, 0, ct);
                    var allIds  = result.AffectedTasks.Select(t => t.TaskId).ToHashSet();
                    var affected = commitments.Where(c => allIds.Contains(c.RowKey)).ToList();
                    var score   = impactScorer.Score(result, affected);

                    // Update impact score on the at-risk entity
                    entity.ImpactScore = score;
                    await repo.UpsertAsync(entity, ct);

                    _log.LogInformation(
                        "At-risk: {Task} (user={Hash}), due={Due}, impact={Score}",
                        entity.RowKey, PiiScrubber.HashValue(userId),
                        entity.DueAt?.ToString("yyyy-MM-dd"), score);

                    totalAtRisk++;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to score at-risk task {Task}", entity.RowKey);
                }
            }
        }

        if (totalAtRisk > 0)
            _log.LogInformation("RiskDetector: {Count} at-risk tasks rescored", totalAtRisk);
    }
}
