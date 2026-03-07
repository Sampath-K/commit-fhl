using CommitApi.Entities;
using CommitApi.Repositories;
using CommitApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace CommitApi.Tests.Services;

public class SignalProfileServiceTests
{
    private readonly Mock<IFeedbackRepository> _feedbackRepo = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly SignalProfileService _sut;

    public SignalProfileServiceTests()
    {
        _sut = new SignalProfileService(
            _feedbackRepo.Object,
            _cache,
            NullLogger<SignalProfileService>.Instance);
    }

    [Fact]
    public async Task GetProfileAsync_NoFeedback_ReturnsDefault()
    {
        SetupFeedback("user1", []);
        var profile = await _sut.GetProfileAsync("user1");
        profile.ConfidenceAdjustment.Should().Be(0.0);
        profile.SuppressedFingerprints.Should().BeEmpty();
        profile.NlpNegativeExamples.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileAsync_FpRate31Percent_ReturnsSmallPenalty()
    {
        // 4 FP out of 13 = 30.7% ≈ 31% > threshold
        var feedback = CreateFeedback(4, "FalsePositive")
            .Concat(CreateFeedback(9, "Confirm"))
            .ToList();
        SetupFeedback("user2", feedback);

        var profile = await _sut.GetProfileAsync("user2");

        profile.ConfidenceAdjustment.Should().Be(0.05);
    }

    [Fact]
    public async Task GetProfileAsync_FpRate61Percent_ReturnsLargePenalty()
    {
        // 7 FP out of 11 = 63.6% > 60% threshold
        var feedback = CreateFeedback(7, "FalsePositive")
            .Concat(CreateFeedback(4, "Confirm"))
            .ToList();
        SetupFeedback("user3", feedback);

        var profile = await _sut.GetProfileAsync("user3");

        profile.ConfidenceAdjustment.Should().Be(0.15);
    }

    [Fact]
    public async Task GetProfileAsync_SuppressedFingerprintsPopulated()
    {
        var fp = new FeedbackEntity
        {
            PartitionKey       = "hash",
            RowKey             = Guid.NewGuid().ToString(),
            FeedbackType       = "FalsePositive",
            TitleFingerprint   = "fp-abc123",
            SourceType         = "Chat",
            RecordedAt         = DateTimeOffset.UtcNow,
            ConfidenceAtFeedback = 0.7,
        };
        SetupFeedback("user4", [fp]);

        var profile = await _sut.GetProfileAsync("user4");

        profile.SuppressedFingerprints.Should().Contain("fp-abc123");
    }

    [Fact]
    public async Task GetProfileAsync_CacheHit_ReturnsSameObject()
    {
        SetupFeedback("user5", []);

        var p1 = await _sut.GetProfileAsync("user5");
        var p2 = await _sut.GetProfileAsync("user5");

        p1.Should().BeSameAs(p2);
        _feedbackRepo.Verify(x => x.GetByUserHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCache_CausesReload()
    {
        SetupFeedback("user6", []);

        await _sut.GetProfileAsync("user6");
        _sut.InvalidateCache("user6");
        await _sut.GetProfileAsync("user6");

        _feedbackRepo.Verify(x => x.GetByUserHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetProfileAsync_NlpExamples_CappedAt5()
    {
        // 10 FP items from different source types → examples should be capped at 5
        var feedback = Enumerable.Range(1, 10)
            .Select(i => new FeedbackEntity
            {
                PartitionKey = "hash",
                RowKey       = Guid.NewGuid().ToString(),
                FeedbackType = "FalsePositive",
                SourceType   = $"Source{i}",
                TitleFingerprint = $"fp-{i}",
                RecordedAt   = DateTimeOffset.UtcNow,
                ConfidenceAtFeedback = 0.7,
            })
            .ToList();
        SetupFeedback("user7", feedback);

        var profile = await _sut.GetProfileAsync("user7");

        profile.NlpNegativeExamples.Count.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task GetProfileAsync_OldFeedbackExcluded()
    {
        // Feedback older than 30 days should not affect the profile
        var oldFp = new FeedbackEntity
        {
            PartitionKey       = "hash",
            RowKey             = Guid.NewGuid().ToString(),
            FeedbackType       = "FalsePositive",
            TitleFingerprint   = "old-fp",
            SourceType         = "Chat",
            RecordedAt         = DateTimeOffset.UtcNow.AddDays(-31),
            ConfidenceAtFeedback = 0.7,
        };
        SetupFeedback("user8", [oldFp]);

        var profile = await _sut.GetProfileAsync("user8");

        profile.ConfidenceAdjustment.Should().Be(0.0);
        profile.SuppressedFingerprints.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileAsync_ZeroFpRate_ZeroAdjustment()
    {
        var feedback = CreateFeedback(5, "Confirm").ToList();
        SetupFeedback("user9", feedback);

        var profile = await _sut.GetProfileAsync("user9");

        profile.ConfidenceAdjustment.Should().Be(0.0);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private void SetupFeedback(string userId, IEnumerable<FeedbackEntity> entities)
    {
        // SignalProfileService calls PiiScrubber.HashValue(userId) — just match any string
        _feedbackRepo
            .Setup(x => x.GetByUserHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities.ToList());
    }

    private static IEnumerable<FeedbackEntity> CreateFeedback(int count, string type)
        => Enumerable.Range(1, count).Select(_ => new FeedbackEntity
        {
            PartitionKey       = "hash",
            RowKey             = Guid.NewGuid().ToString(),
            FeedbackType       = type,
            SourceType         = "Chat",
            TitleFingerprint   = Guid.NewGuid().ToString()[..8],
            RecordedAt         = DateTimeOffset.UtcNow,
            ConfidenceAtFeedback = 0.7,
        });
}
