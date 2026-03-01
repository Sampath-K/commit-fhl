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
}
