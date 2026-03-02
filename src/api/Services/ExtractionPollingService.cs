using CommitApi.Config;

namespace CommitApi.Services;

/// <summary>
/// Background service that polls for new commitments on a 5-minute schedule.
///
/// For each user who has an active (non-expired) OBO token in the TokenCache
/// AND whose last extraction was more than <see cref="MinExtractionInterval"/> ago,
/// runs a full extraction via <see cref="IExtractionOrchestrator"/>.
///
/// Token lifecycle:
///   - Tokens are stored by authenticated endpoints (extract, commitments GET, subscriptions)
///   - Tokens expire after ~1 hour (standard AAD OBO TTL)
///   - Once a token expires, that user is no longer polled until they re-authenticate
///
/// This enables D-001 metric 2: "End-to-end latency &lt; 5 min — meeting ends → commitment in Teams pane".
/// </summary>
public sealed class ExtractionPollingService : BackgroundService
{
    private static readonly TimeSpan PollingInterval        = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinExtractionInterval  = TimeSpan.FromMinutes(3);

    private readonly TokenCache                          _tokenCache;
    private readonly IExtractionOrchestrator             _orchestrator;
    private readonly ILogger<ExtractionPollingService>   _logger;

    public ExtractionPollingService(
        TokenCache                          tokenCache,
        IExtractionOrchestrator             orchestrator,
        ILogger<ExtractionPollingService>   logger)
    {
        _tokenCache   = tokenCache;
        _orchestrator = orchestrator;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExtractionPollingService started — polling every {Interval}", PollingInterval);

        using var timer = new PeriodicTimer(PollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollAllActiveUsersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExtractionPollingService: unhandled error in poll cycle");
            }
        }

        _logger.LogInformation("ExtractionPollingService stopped");
    }

    private async Task PollAllActiveUsersAsync(CancellationToken ct)
    {
        var activeUsers = _tokenCache.GetActiveUserIds().ToList();
        if (activeUsers.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var eligible = activeUsers
            .Select(uid => (uid, entry: _tokenCache.Get(uid)))
            .Where(x => x.entry is not null)
            .Where(x => x.entry!.LastExtracted is null ||
                        now - x.entry.LastExtracted.Value >= MinExtractionInterval)
            .ToList();

        if (eligible.Count == 0) return;

        _logger.LogInformation(
            "ExtractionPollingService: polling {Count} eligible user(s) (of {Active} active)",
            eligible.Count, activeUsers.Count);

        // Run all eligible users concurrently (bounded by Graph API rate limits)
        var tasks = eligible.Select(async x =>
        {
            var (userId, entry) = x;
            try
            {
                var count = await _orchestrator.ExtractAndStoreAsync(userId, entry!.Token, ct);
                _tokenCache.MarkExtracted(userId);

                if (count > 0)
                    _logger.LogInformation(
                        "ExtractionPollingService: {Count} new item(s) for user {Hash}",
                        count, PiiScrubber.HashValue(userId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ExtractionPollingService: extraction failed for user {Hash} — will retry next cycle",
                    PiiScrubber.HashValue(userId));
            }
        });

        await Task.WhenAll(tasks);
    }
}
