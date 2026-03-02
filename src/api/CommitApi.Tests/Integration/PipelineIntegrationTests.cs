using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Graph;
using CommitApi.Models.Extraction;
using CommitApi.Models.Graph;
using CommitApi.Replan;
using CommitApi.Repositories;
using CommitApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Integration;

/// <summary>
/// T-035 — Integration test suite covering the full Commit pipeline:
/// signal extraction → commitment storage → cascade simulation → replan generation → approval.
/// All 15 tests must pass. (P-06: ≥90% coverage, Stryker ≥80%)
/// </summary>
public class PipelineIntegrationTests
{
    // ─── Shared helpers ────────────────────────────────────────────────────────

    private const string UserId = "integration-user-01";

    private static CommitmentEntity MakeCommitment(
        string rowKey,
        string title,
        string status     = "pending",
        string[] blocks   = null!,
        DateTimeOffset? dueAt = null,
        int watcherCount  = 0,
        int impactScore   = 0)
    {
        var watchers = Enumerable.Range(0, watcherCount)
            .Select(i => $"watcher-{i}")
            .ToList();
        return new CommitmentEntity
        {
            PartitionKey  = UserId,
            RowKey        = rowKey,
            Title         = title,
            Owner         = UserId,
            WatchersJson  = System.Text.Json.JsonSerializer.Serialize(watchers),
            BlocksJson    = System.Text.Json.JsonSerializer.Serialize(blocks ?? Array.Empty<string>()),
            BlockedByJson = "[]",
            CommittedAt   = DateTimeOffset.UtcNow.AddDays(-1),
            DueAt         = dueAt ?? DateTimeOffset.UtcNow.AddDays(3),
            Status        = status,
            ImpactScore   = impactScore,
            Priority      = "medium",
            SourceType    = "Chat",
        };
    }

    private static RawCommitment MakeRaw(
        string title,
        CommitmentSourceType sourceType = CommitmentSourceType.Chat,
        DateTimeOffset? dueAt = null,
        string[]? watchers = null,
        double confidence = 0.8) =>
        new(
            Title:          title,
            OwnerUserId:    UserId,
            OwnerDisplayName: "Integration Test User",
            SourceType:     sourceType,
            SourceUrl:      "https://teams.microsoft.com/fake",
            ExtractedAt:    DateTimeOffset.UtcNow,
            DueAt:          dueAt,
            Confidence:     confidence,
            WatcherUserIds: watchers ?? [],
            SourceContext:  string.Empty);

    // ─── Test 1: EisenhowerScorer — urgent task (due < 48hrs) ────────────────

    [Fact]
    public void Test01_EisenhowerScorer_UrgentDue_AssignsUrgentOrDelegate()
    {
        var scorer = new EisenhowerScorer();
        var raw    = MakeRaw("Deploy hotfix", dueAt: DateTimeOffset.UtcNow.AddHours(10));  // < 48hrs → urgent

        var priority = scorer.Score(raw);

        priority.Should().NotBeNullOrEmpty();
        // Due < 48hrs → urgent quadrant. With no watchers, ADO, or high confidence it's "delegate"
        priority.Should().BeOneOf(
            new[] { "urgent-important", "delegate" },
            "due in < 48hrs must be placed in an urgent quadrant");
    }

    // ─── Test 2: EisenhowerScorer — not urgent but important (2+ watchers) ───

    [Fact]
    public void Test02_EisenhowerScorer_TwoWatchers_AssignsSchedule()
    {
        var scorer = new EisenhowerScorer();
        var raw    = MakeRaw(
            title:     "Architecture review",
            dueAt:     DateTimeOffset.UtcNow.AddDays(10),          // not urgent
            watchers:  ["watcher-a", "watcher-b"],                 // 2+ → important
            confidence: 0.6);

        var priority = scorer.Score(raw);

        priority.Should().Be("schedule",
            because: "not-urgent + important (2+ watchers) maps to the Schedule quadrant");
    }

    // ─── Test 3: DeduplicationService — identical title idempotent ───────────

    [Fact]
    public void Test03_Deduplication_SameTitle_ProducesSingleCommitment()
    {
        var dedup = new DeduplicationService(NullLogger<DeduplicationService>.Instance);
        var first  = MakeRaw("Review PR #123", sourceType: CommitmentSourceType.Ado,  confidence: 0.85);
        var second = MakeRaw("Review PR #123", sourceType: CommitmentSourceType.Chat, confidence: 0.75);
        var duplicates = new List<RawCommitment> { first, second };

        var result = dedup.Deduplicate(duplicates);

        result.Should().HaveCount(1, because: "identical title from two sources should merge to one");
    }

    // ─── Test 4: DeduplicationService — different titles both preserved ───────

    [Fact]
    public void Test04_Deduplication_DifferentTitles_BothPreserved()
    {
        var dedup = new DeduplicationService(NullLogger<DeduplicationService>.Instance);
        var commitments = new List<RawCommitment>
        {
            MakeRaw("Write unit tests for extractor", sourceType: CommitmentSourceType.Email, confidence: 0.7),
            MakeRaw("Deploy production build v2.1",   sourceType: CommitmentSourceType.Ado,   confidence: 0.8),
        };

        var result = dedup.Deduplicate(commitments);

        result.Should().HaveCount(2, because: "semantically different titles must both be kept");
    }

    // ─── Test 5: CascadeSimulator — 5-task chain propagates slip ─────────────

    [Fact]
    public async Task Test05_CascadeSimulator_FiveTaskChain_PropagatesSlip()
    {
        var now = DateTimeOffset.UtcNow.Date;
        // B and C share A's original due date so slipping A by 2 pushes them both.
        // D is at now+3, exactly equal to B's new ETA — strict-greater check stops propagation there.
        // Result: A, B, C affected (3 tasks ≥ 3 assertion).
        var entities = new List<CommitmentEntity>
        {
            MakeCommitment("A", "API design",       blocks: ["B"], dueAt: now.AddDays(1)),
            MakeCommitment("B", "Backend impl",     blocks: ["C"], dueAt: now.AddDays(1)),  // same day as A
            MakeCommitment("C", "Frontend wiring",  blocks: ["D"], dueAt: now.AddDays(2)),
            MakeCommitment("D", "Integration test", blocks: ["E"], dueAt: now.AddDays(3)),
            MakeCommitment("E", "Demo deploy",      blocks: [],    dueAt: now.AddDays(10)),
        };

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entities);
        repoMock.Setup(r => r.GetAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) =>
                    entities.FirstOrDefault(e => e.RowKey == id));

        var simulator = new CascadeSimulator(repoMock.Object, NullLogger<CascadeSimulator>.Instance);
        var result    = await simulator.SimulateAsync("A", UserId, slipDays: 2);

        result.AffectedTasks.Should().Contain(t => t.TaskId == "A",
            because: "root task must be in affected list");
        result.AffectedTasks.Should().Contain(t => t.TaskId == "B",
            because: "direct dependent B must slip");
        result.TotalTasksAffected.Should().BeGreaterThanOrEqualTo(3,
            because: "at least A, B, C should slip with a 2-day slip on A");
    }

    // ─── Test 6: CascadeSimulator — no downstream tasks, only root affected ──

    [Fact]
    public async Task Test06_CascadeSimulator_LeafTask_OnlyRootAffected()
    {
        var entity   = MakeCommitment("root-only", "Standalone task", blocks: []);
        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CommitmentEntity> { entity });
        repoMock.Setup(r => r.GetAsync(UserId, "root-only", It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);

        var simulator = new CascadeSimulator(repoMock.Object, NullLogger<CascadeSimulator>.Instance);
        var result    = await simulator.SimulateAsync("root-only", UserId, slipDays: 1);

        result.TotalTasksAffected.Should().Be(1);
        result.AffectedTasks.Should().ContainSingle(t => t.TaskId == "root-only");
    }

    // ─── Test 7: ImpactScorer — score within valid range (0–100) ─────────────

    [Fact]
    public async Task Test07_ImpactScorer_ValidRange_ZeroToHundred()
    {
        var now = DateTimeOffset.UtcNow.Date;
        var entities = new List<CommitmentEntity>
        {
            MakeCommitment("X", "Exec presentation", watcherCount: 5, dueAt: now.AddDays(1)),
            MakeCommitment("Y", "Dependent task",    blocks: [],      dueAt: now.AddDays(2)),
        };

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entities);
        repoMock.Setup(r => r.GetAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) =>
                    entities.FirstOrDefault(e => e.RowKey == id));

        var simulator    = new CascadeSimulator(repoMock.Object, NullLogger<CascadeSimulator>.Instance);
        var cascadeResult = await simulator.SimulateAsync("X", UserId, slipDays: 1);

        var scorer = new ImpactScorer();
        var score  = scorer.Score(cascadeResult, entities);

        score.Should().BeInRange(0, 100, because: "impact score is capped 0–100 by formula");
    }

    // ─── Test 8: ImpactScorer — 5-task chain score is in expected band ────────

    [Fact]
    public async Task Test08_ImpactScorer_FiveTaskChain_ScoreInExpectedBand()
    {
        var now = DateTimeOffset.UtcNow.Date;
        // 0 watchers per task → no people-contribution (avoids exec-visibility spike).
        // All due tomorrow → with slipDays=5 all 5 cascade (newEta clearly > dueAt for each).
        // Score formula: 0 (people) + 50 (5 tasks × 2 calHrs × 5) + 0 (exec) - 0 (time) ≈ 50.
        var entities = new List<CommitmentEntity>
        {
            MakeCommitment("P", "Planning",     blocks: ["Q"], watcherCount: 0, dueAt: now.AddDays(1)),
            MakeCommitment("Q", "Dev",          blocks: ["R"], watcherCount: 0, dueAt: now.AddDays(1)),
            MakeCommitment("R", "Test",         blocks: ["S"], watcherCount: 0, dueAt: now.AddDays(1)),
            MakeCommitment("S", "Deploy",       blocks: ["T"], watcherCount: 0, dueAt: now.AddDays(1)),
            MakeCommitment("T", "Post-release", blocks: [],    watcherCount: 0, dueAt: now.AddDays(1)),
        };

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entities);
        repoMock.Setup(r => r.GetAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) =>
                    entities.FirstOrDefault(e => e.RowKey == id));

        var simulator     = new CascadeSimulator(repoMock.Object, NullLogger<CascadeSimulator>.Instance);
        var cascadeResult = await simulator.SimulateAsync("P", UserId, slipDays: 5);
        var scorer        = new ImpactScorer();

        // Mirror Program.cs: only pass cascade-affected entities to the scorer
        var affectedIds      = cascadeResult.AffectedTasks.Select(t => t.TaskId)
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affectedEntities = entities.Where(e => affectedIds.Contains(e.RowKey)).ToList();
        var score            = scorer.Score(cascadeResult, affectedEntities);

        // T-022 acceptance criterion: score between 30–60 for a 5-task synthetic chain
        score.Should().BeInRange(30, 60,
            because: "spec mandates 5-task test chain score in 30–60 band (T-022 acceptance criterion)");
    }

    // ─── Test 9: ReplanGenerator — returns 3 distinct options ─────────────────

    [Fact]
    public async Task Test09_ReplanGenerator_ThreeOptionsWithDistinctConfidence()
    {
        var cascadeResult = new CascadeResult(
            RootTaskId:           "chain-root",
            InputSlipDays:        2,
            AffectedTasks:
            [
                new AffectedTask("chain-root", "Root task",   2, null, DateTimeOffset.UtcNow.AddDays(4), 0.0),
                new AffectedTask("dep-1",      "Dependent 1", 2, null, DateTimeOffset.UtcNow.AddDays(5), 0.0),
                new AffectedTask("dep-2",      "Dependent 2", 2, null, DateTimeOffset.UtcNow.AddDays(6), 0.0),
            ],
            TotalCalendarPressure: 0.0);

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CommitmentEntity>());
        repoMock.Setup(r => r.GetAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommitmentEntity?)null);

        var generator = new ReplanGenerator(repoMock.Object, NullLogger<ReplanGenerator>.Instance);
        var options   = await generator.GenerateAsync(cascadeResult, UserId);

        options.Should().HaveCount(3, because: "spec requires exactly 3 replan options (A/B/C)");
        options.Select(o => o.OptionId).Should().OnlyHaveUniqueItems(because: "each option must have a unique ID");
        options.Select(o => o.Confidence).Should().OnlyHaveUniqueItems(
            because: "T-026 acceptance criterion: options must have different confidence levels");
        options.All(o => o.Confidence is >= 0.0 and <= 1.0).Should().BeTrue(
            because: "confidence is a probability in [0,1]");
    }

    // ─── Test 10: PiiScrubber — no raw user identifiers in output ─────────────

    [Fact]
    public void Test10_PiiScrubber_HashesUserId_NoRawValueInOutput()
    {
        const string rawUserId = "SampathK@7k2cc2.onmicrosoft.com";

        var hashed = PiiScrubber.HashValue(rawUserId);

        hashed.Should().NotBe(rawUserId, because: "raw user ID must be replaced by hash");
        hashed.Should().HaveLength(16, because: "PiiScrubber takes the first 16 hex chars (SHA-256 prefix) for log readability");
        hashed.Should().MatchRegex("^[0-9a-f]+$", because: "hash must be lowercase hex");
    }

    // ─── Test 11: PiiScrubber — deterministic (same input → same hash) ────────

    [Fact]
    public void Test11_PiiScrubber_SameInput_ProducesSameHash()
    {
        const string userId = "alice@contoso.com";

        var hash1 = PiiScrubber.HashValue(userId);
        var hash2 = PiiScrubber.HashValue(userId);

        hash1.Should().Be(hash2, because: "SHA-256 is deterministic — same input = same output");
    }

    // ─── Test 12: FeatureFlagService — psychologyLayer enabled in dev ─────────

    [Fact]
    public async Task Test12_FeatureFlagService_PsychologyLayer_EnabledInDev()
    {
        using var cache  = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(
            appConfigClient: null,
            cache:           cache,
            logger:          NullLogger<FeatureFlagService>.Instance,
            environment:     "dev");

        var enabled = await service.IsEnabledAsync("commit.feature.psychologyLayer");

        enabled.Should().BeTrue(
            because: "T-C01 acceptance criterion: psychologyLayer must be enabled in dev environment");
    }

    // ─── Test 13: FeatureFlagService — psychologyLayer disabled in pilot ───────

    [Fact]
    public async Task Test13_FeatureFlagService_PsychologyLayer_DisabledInPilot()
    {
        using var cache  = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(
            appConfigClient: null,
            cache:           cache,
            logger:          NullLogger<FeatureFlagService>.Instance,
            environment:     "pilot");

        var enabled = await service.IsEnabledAsync("commit.feature.psychologyLayer");

        enabled.Should().BeFalse(
            because: "T-C01 acceptance criterion: psychologyLayer must be false in pilot");
    }

    // ─── Test 14: MotivationService — delivery score in valid range ───────────

    [Fact]
    public async Task Test14_MotivationService_DeliveryScore_ValidRange()
    {
        var entities = new List<CommitmentEntity>
        {
            MakeCommitment("d1", "Done task 1",    status: "done",    dueAt: DateTimeOffset.UtcNow.AddDays(-2)),
            MakeCommitment("d2", "Done task 2",    status: "done",    dueAt: DateTimeOffset.UtcNow.AddDays(-1)),
            MakeCommitment("p1", "Pending task 1", status: "pending", dueAt: DateTimeOffset.UtcNow.AddDays(2)),
        };

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entities);

        var svc   = new MotivationService(repoMock.Object, NullLogger<MotivationService>.Instance);
        var state = await svc.GetStateAsync(UserId);

        state.DeliveryScore.Should().BeInRange(0, 100,
            because: "delivery score is always clamped 0–100");
        state.UserId.Should().Be(UserId);
        state.TotalXp.Should().BeGreaterThanOrEqualTo(0, because: "XP is non-negative");
        state.CompetencyLevel.Should().BeInRange(1, 5, because: "level is 1–5");
    }

    // ─── Test 15: Full pipeline integration — extract → cascade → replan ──────

    [Fact]
    public async Task Test15_FullPipeline_ExtractCascadeReplan_EndToEnd()
    {
        // ── Arrange: 3 at-risk tasks in a dependency chain ─────────────────────
        var now = DateTimeOffset.UtcNow.Date;
        var commitments = new List<CommitmentEntity>
        {
            MakeCommitment("alpha", "API spec due today",   blocks: ["beta"],  watcherCount: 3,
                dueAt: now.AddHours(6),  impactScore: 0),
            MakeCommitment("beta",  "Frontend integration", blocks: ["gamma"], watcherCount: 2,
                dueAt: now.AddDays(1),   impactScore: 0),
            MakeCommitment("gamma", "Demo readiness",       blocks: [],        watcherCount: 4,
                dueAt: now.AddDays(2),   impactScore: 0),
        };

        var repoMock = new Mock<ICommitmentRepository>();
        repoMock.Setup(r => r.ListByOwnerAsync(UserId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(commitments);
        repoMock.Setup(r => r.GetAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) =>
                    commitments.FirstOrDefault(e => e.RowKey == id));
        repoMock.Setup(r => r.UpsertAsync(It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var simulator = new CascadeSimulator(repoMock.Object, NullLogger<CascadeSimulator>.Instance);
        var scorer    = new ImpactScorer();
        var replan    = new ReplanGenerator(repoMock.Object, NullLogger<ReplanGenerator>.Instance);

        // ── Act: simulate cascade from root ────────────────────────────────────
        var cascadeResult = await simulator.SimulateAsync("alpha", UserId, slipDays: 1);
        var impactScore   = scorer.Score(cascadeResult, commitments);
        var options       = await replan.GenerateAsync(cascadeResult, UserId);

        // ── Assert: cascade propagated ─────────────────────────────────────────
        cascadeResult.AffectedTasks.Should().Contain(t => t.TaskId == "alpha",
            because: "root must appear in affected list");
        cascadeResult.TotalTasksAffected.Should().BeGreaterThanOrEqualTo(1);

        // Impact score must be valid
        impactScore.Should().BeInRange(0, 100,
            because: "impact score always capped 0–100");

        // Replan options must be complete
        options.Should().HaveCount(3, because: "replan generator always produces A, B, C options");
        options.Should().AllSatisfy(o =>
        {
            o.Label.Should().NotBeNullOrEmpty("each option must have a human-readable label");
            o.Description.Should().NotBeNullOrEmpty("each option must explain the approach");
            o.RequiredActions.Should().NotBeNull("required actions list must exist (may be empty)");
        });
    }
}
