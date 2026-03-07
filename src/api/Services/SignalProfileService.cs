using System.Security.Cryptography;
using System.Text;
using CommitApi.Config;
using CommitApi.Models.Feedback;
using CommitApi.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace CommitApi.Services;

/// <summary>
/// Derives a per-user <see cref="UserSignalProfile"/> from the last 30 days of feedback.
/// Results are cached in IMemoryCache for 5 minutes per user.
/// </summary>
public sealed class SignalProfileService : ISignalProfileService
{
    private readonly IFeedbackRepository _feedbackRepo;
    private readonly IMemoryCache        _cache;
    private readonly ILogger<SignalProfileService> _logger;

    private static readonly TimeSpan CacheTtl      = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(30);

    // Thresholds for confidence adjustment
    private const double FpRateSmallPenalty  = 0.30;  // FP rate > 30% → small penalty
    private const double FpRateLargePenalty  = 0.60;  // FP rate > 60% → large penalty
    private const double SmallPenaltyAmount  = 0.05;
    private const double LargePenaltyAmount  = 0.15;
    private const int    MaxNlpExamples      = 5;

    public SignalProfileService(
        IFeedbackRepository feedbackRepo,
        IMemoryCache cache,
        ILogger<SignalProfileService> logger)
    {
        _feedbackRepo = feedbackRepo;
        _cache        = cache;
        _logger       = logger;
    }

    /// <inheritdoc/>
    public async Task<UserSignalProfile> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var cacheKey = $"signal-profile:{userId}";
        if (_cache.TryGetValue(cacheKey, out UserSignalProfile? cached) && cached is not null)
            return cached;

        var profile = await BuildProfileAsync(userId, ct);
        _cache.Set(cacheKey, profile, CacheTtl);
        return profile;
    }

    /// <inheritdoc/>
    public void InvalidateCache(string userId)
    {
        _cache.Remove($"signal-profile:{userId}");
    }

    private async Task<UserSignalProfile> BuildProfileAsync(string userId, CancellationToken ct)
    {
        var userHash = PiiScrubber.HashValue(userId);
        var allFeedback = await _feedbackRepo.GetByUserHashAsync(userHash, ct);

        var cutoff  = DateTimeOffset.UtcNow - LookbackWindow;
        var recent  = allFeedback.Where(f => f.RecordedAt >= cutoff).ToList();

        if (recent.Count == 0)
            return UserSignalProfile.Default;

        // ── Confidence adjustment ──────────────────────────────────────────────
        var fpCount   = recent.Count(f => f.FeedbackType == nameof(Models.Feedback.FeedbackType.FalsePositive));
        var fpRate    = (double)fpCount / recent.Count;

        var confidenceAdjustment = fpRate > FpRateLargePenalty ? LargePenaltyAmount
                                 : fpRate > FpRateSmallPenalty ? SmallPenaltyAmount
                                 : 0.0;

        // ── Suppressed fingerprints ───────────────────────────────────────────
        var suppressed = recent
            .Where(f => f.FeedbackType == nameof(Models.Feedback.FeedbackType.FalsePositive))
            .Select(f => f.TitleFingerprint)
            .Where(fp => !string.IsNullOrEmpty(fp))
            .ToHashSet();

        // ── NLP examples (heuristic — fingerprints used as opaque tokens) ─────
        // Derive display hints from SourceType groupings (no raw text stored)
        var negExamples = recent
            .Where(f => f.FeedbackType == nameof(Models.Feedback.FeedbackType.FalsePositive))
            .GroupBy(f => f.SourceType)
            .Select(g => $"source:{g.Key} (confirmed non-commitment for this user)")
            .Take(MaxNlpExamples)
            .ToList();

        var posExamples = recent
            .Where(f => f.FeedbackType == nameof(Models.Feedback.FeedbackType.Confirm))
            .GroupBy(f => f.SourceType)
            .Select(g => $"source:{g.Key} (confirmed commitment for this user)")
            .Take(MaxNlpExamples)
            .ToList();

        _logger.LogDebug(
            "SignalProfileService: user {Hash} → fpRate={FpRate:P0}, adjustment={Adj:+0.00;-0.00}",
            userHash, fpRate, confidenceAdjustment);

        return new UserSignalProfile(
            ConfidenceAdjustment:   confidenceAdjustment,
            SuppressedFingerprints: suppressed,
            NlpNegativeExamples:    negExamples,
            NlpPositiveExamples:    posExamples);
    }
}
