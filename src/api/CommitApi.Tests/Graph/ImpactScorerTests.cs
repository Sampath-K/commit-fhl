using CommitApi.Entities;
using CommitApi.Graph;
using CommitApi.Models.Graph;
using Xunit;

namespace CommitApi.Tests.Graph;

public class ImpactScorerTests
{
    private static CommitmentEntity MakeEntity(
        string rowKey,
        string[] watchers,
        string priority       = "not-urgent-not-important",
        DateTimeOffset? dueAt = null) =>
        new()
        {
            PartitionKey  = "u1",
            RowKey        = rowKey,
            Title         = $"Task {rowKey}",
            Owner         = "u1",
            WatchersJson  = System.Text.Json.JsonSerializer.Serialize(watchers),
            BlocksJson    = "[]",
            BlockedByJson = "[]",
            CommittedAt   = DateTimeOffset.UtcNow.AddDays(-1),
            DueAt         = dueAt ?? DateTimeOffset.UtcNow.AddDays(10),
            Priority      = priority,
            Status        = "pending"
        };

    private static CascadeResult MakeCascade(int taskCount, string rootId = "t1") =>
        new(
            RootTaskId:          rootId,
            InputSlipDays:       2,
            AffectedTasks:       Enumerable.Range(1, taskCount)
                .Select(i => new AffectedTask($"t{i}", $"Task t{i}", 2,
                    DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow.AddDays(12), 0.2))
                .ToList(),
            TotalCalendarPressure: 0.6);

    /// <summary>
    /// With 5 tasks, 2 unique watchers (w1+w2), no exec, due in 10 days:
    ///   people=2 → 20, calHrs=10 → 50, exec=0 → 0, days=10 → -20
    ///   total = 50 → within [30, 60].
    /// </summary>
    [Fact]
    public void Score_FiveTaskChain_IsInExpectedRange()
    {
        var scorer   = new ImpactScorer();
        var cascade  = MakeCascade(5);
        var entities = Enumerable.Range(1, 5)
            .Select(i => MakeEntity($"t{i}", ["w1", "w2"],
                dueAt: DateTimeOffset.UtcNow.AddDays(10)))
            .ToList();

        var score = scorer.Score(cascade, entities);

        Assert.InRange(score, 30, 60);
    }

    [Fact]
    public void Score_NoWatchers_NoExec_StillReturnsNonNegative()
    {
        var scorer   = new ImpactScorer();
        var cascade  = MakeCascade(1);
        var entities = new[] { MakeEntity("t1", [], dueAt: DateTimeOffset.UtcNow.AddDays(1)) };

        var score = scorer.Score(cascade, entities);

        Assert.True(score >= 0, $"Score should not be negative but got {score}");
    }

    [Fact]
    public void Score_ExecVisibleTask_IncreasesScoreByTwenty()
    {
        var scorer = new ImpactScorer();

        // Two identical cascades — one with exec-visible priority, one without
        var cascade = MakeCascade(1);

        var noExec  = new[] { MakeEntity("t1", ["w1"], "not-urgent-not-important",
            dueAt: DateTimeOffset.UtcNow.AddDays(10)) };
        var withExec = new[] { MakeEntity("t1", ["w1"], "urgent-important",
            dueAt: DateTimeOffset.UtcNow.AddDays(10)) };

        var scoreNoExec   = scorer.Score(cascade, noExec);
        var scoreWithExec = scorer.Score(cascade, withExec);

        Assert.True(scoreWithExec > scoreNoExec,
            $"Exec-visible task score ({scoreWithExec}) should exceed non-exec ({scoreNoExec})");
        Assert.True(scoreWithExec - scoreNoExec >= 15,
            "Exec visibility contribution should be at least 15 points");
    }

    [Fact]
    public void Score_OverdueTasks_DaysContributionIsZeroNotNegative()
    {
        var scorer  = new ImpactScorer();
        var cascade = MakeCascade(1);
        // Due in the past — daysToDateDep should clamp to 0
        var entities = new[] { MakeEntity("t1", ["w1"],
            dueAt: DateTimeOffset.UtcNow.AddDays(-5)) };

        var score = scorer.Score(cascade, entities);

        Assert.True(score >= 0, $"Overdue score should be ≥0 but got {score}");
    }

    [Fact]
    public void Score_EmptyEntities_ReturnsZero()
    {
        var scorer  = new ImpactScorer();
        var cascade = MakeCascade(0);

        var score = scorer.Score(cascade, []);

        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_AlwaysCappedAt100()
    {
        var scorer  = new ImpactScorer();
        var cascade = MakeCascade(10);
        // 20 unique watchers × 10 = 200 → capped at 100
        var entities = Enumerable.Range(1, 10)
            .Select(i => MakeEntity($"t{i}", Enumerable.Range(1, 20).Select(j => $"w{j}").ToArray(),
                "urgent-important", dueAt: DateTimeOffset.UtcNow.AddDays(1)))
            .ToList();

        var score = scorer.Score(cascade, entities);

        Assert.Equal(100, score);
    }
}
