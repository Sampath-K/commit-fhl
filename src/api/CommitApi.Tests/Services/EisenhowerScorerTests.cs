using CommitApi.Models.Extraction;
using CommitApi.Services;
using Xunit;

namespace CommitApi.Tests.Services;

public class EisenhowerScorerTests
{
    private readonly EisenhowerScorer _sut = new();

    private static RawCommitment Make(
        DateTimeOffset? dueAt = null,
        string[] watchers = null!,
        CommitmentSourceType source = CommitmentSourceType.Chat,
        double confidence = 0.7)
        => new(
            Title:            "Some task",
            OwnerUserId:      "user-a",
            OwnerDisplayName: "User A",
            SourceType:       source,
            SourceUrl:        "",
            ExtractedAt:      DateTimeOffset.UtcNow,
            DueAt:            dueAt,
            Confidence:       confidence,
            WatcherUserIds:   watchers ?? [],
            SourceContext:    "ctx");

    [Fact]
    public void Score_DueIn24Hrs_ManyWatchers_ReturnsUrgentImportant()
    {
        var commitment = Make(dueAt: DateTimeOffset.UtcNow.AddHours(24), watchers: ["w1", "w2"]);
        var result     = _sut.Score(commitment);
        Assert.Equal("urgent-important", result);
    }

    [Fact]
    public void Score_DueIn3Days_ManyWatchers_ReturnsSchedule()
    {
        var commitment = Make(dueAt: DateTimeOffset.UtcNow.AddDays(3), watchers: ["w1", "w2"]);
        var result     = _sut.Score(commitment);
        Assert.Equal("schedule", result);
    }

    [Fact]
    public void Score_DueIn24Hrs_NoWatchers_ReturnsDelegate()
    {
        var commitment = Make(dueAt: DateTimeOffset.UtcNow.AddHours(12));
        var result     = _sut.Score(commitment);
        Assert.Equal("delegate", result);
    }

    [Fact]
    public void Score_NoDueDate_NoWatchers_ReturnsDefer()
    {
        var commitment = Make();
        var result     = _sut.Score(commitment);
        Assert.Equal("defer", result);
    }

    [Fact]
    public void Score_AdoSource_NoDeadline_ReturnsSchedule()
    {
        // ADO review is always important regardless of watchers
        var commitment = Make(source: CommitmentSourceType.Ado);
        var result     = _sut.Score(commitment);
        Assert.Equal("schedule", result);
    }

    [Fact]
    public void Score_HighConfidenceTranscript_ReturnsSchedule()
    {
        // High-confidence meeting transcript → important even without watchers
        var commitment = Make(source: CommitmentSourceType.Transcript, confidence: 0.9);
        var result     = _sut.Score(commitment);
        Assert.Equal("schedule", result);
    }

    [Fact]
    public void Score_DueIn47Hrs_IsUrgent()
    {
        // 47 hours < 48 threshold → urgent
        var commitment = Make(dueAt: DateTimeOffset.UtcNow.AddHours(47));
        var result     = _sut.Score(commitment);
        Assert.Equal("delegate", result); // urgent=true, important=false
    }

    [Fact]
    public void Score_DueIn49Hrs_IsNotUrgent()
    {
        // 49 hours > 48 threshold → not urgent
        var commitment = Make(dueAt: DateTimeOffset.UtcNow.AddHours(49));
        var result     = _sut.Score(commitment);
        Assert.Equal("defer", result); // urgent=false, important=false
    }

    [Fact]
    public void Score_Exactly1Watcher_NotImportant()
    {
        // 1 watcher < 2 threshold → not important (no due date)
        var commitment = Make(watchers: ["w1"]);
        var result     = _sut.Score(commitment);
        Assert.Equal("defer", result); // urgent=false, important=false
    }

    [Fact]
    public void Score_Exactly2Watchers_IsImportant()
    {
        // 2 watchers >= 2 threshold → important
        var commitment = Make(watchers: ["w1", "w2"]);
        var result     = _sut.Score(commitment);
        Assert.Equal("schedule", result); // urgent=false, important=true
    }
}
