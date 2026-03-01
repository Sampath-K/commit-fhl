using CommitApi.Entities;
using CommitApi.Graph;
using CommitApi.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Graph;

public class DependencyLinkerTests
{
    private static CommitmentEntity MakeEntity(
        string rowKey,
        string title,
        string? sourceId       = null,
        string[]? watchers     = null,
        string userId          = "u1",
        DateTimeOffset? dueAt  = null) =>
        new()
        {
            PartitionKey  = userId,
            RowKey        = rowKey,
            Title         = title,
            Owner         = userId,
            SourceId      = sourceId,
            WatchersJson  = System.Text.Json.JsonSerializer.Serialize(watchers ?? []),
            BlocksJson    = "[]",
            BlockedByJson = "[]",
            CommittedAt   = DateTimeOffset.UtcNow.AddHours(-1),
            DueAt         = dueAt ?? DateTimeOffset.UtcNow.AddDays(3),
            Status        = "pending"
        };

    private static DependencyLinker BuildLinker(ICommitmentRepository repo) =>
        new(repo, NullLogger<DependencyLinker>.Instance);

    // ── Thread signal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildGraph_SameSourceId_CreatesThreadEdge()
    {
        var a = MakeEntity("t1", "Deploy service", sourceId: "meeting-123");
        var b = MakeEntity("t2", "Review deployment", sourceId: "meeting-123");

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);
        mock.Setup(r => r.UpsertAsync(It.IsAny<CommitmentEntity>(), default))
            .Returns(Task.CompletedTask);

        var linker = BuildLinker(mock.Object);
        var edges  = await linker.BuildGraphAsync("u1", "token");

        Assert.Single(edges);
        Assert.Equal("thread", edges[0].EdgeType);
        Assert.Equal("t1", edges[0].FromId);
        Assert.Equal("t2", edges[0].ToId);
        Assert.Equal(0.90, edges[0].Confidence, precision: 2);
    }

    // ── People signal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildGraph_OverlappingWatchers_CreatesPeopleEdge()
    {
        var a = MakeEntity("t1", "Write design doc",  watchers: ["alice", "bob"]);
        var b = MakeEntity("t2", "Implement feature", watchers: ["bob", "carol"]);

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);
        mock.Setup(r => r.UpsertAsync(It.IsAny<CommitmentEntity>(), default))
            .Returns(Task.CompletedTask);

        var linker = BuildLinker(mock.Object);
        var edges  = await linker.BuildGraphAsync("u1", "token");

        Assert.Single(edges);
        Assert.Equal("people", edges[0].EdgeType);
        Assert.True(edges[0].Confidence > 0.60);
    }

    // ── Title signal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildGraph_SimilarTitles_CreatesTitleEdge()
    {
        // 6 tokens each (stopword "for" removed), 1 unique each → 5/7 = 0.714 Jaccard
        // A tokens: review, pr, authentication, service, code, fix
        // B tokens: review, pr, authentication, service, code, merge
        var a = MakeEntity("t1", "Review PR for authentication service code fix");
        var b = MakeEntity("t2", "Review PR for authentication service code merge");

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);
        mock.Setup(r => r.UpsertAsync(It.IsAny<CommitmentEntity>(), default))
            .Returns(Task.CompletedTask);

        var linker = BuildLinker(mock.Object);
        var edges  = await linker.BuildGraphAsync("u1", "token");

        Assert.Single(edges);
        Assert.Equal("title", edges[0].EdgeType);
        Assert.True(edges[0].Confidence >= 0.70);
    }

    // ── No signal ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildGraph_NoSignals_ProducesNoEdges()
    {
        var a = MakeEntity("t1", "Fix login bug",     watchers: ["alice"]);
        var b = MakeEntity("t2", "Update deployment", watchers: ["bob"],   sourceId: "chat-999");

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);

        var linker = BuildLinker(mock.Object);
        var edges  = await linker.BuildGraphAsync("u1", "token");

        Assert.Empty(edges);
    }

    // ── Jaccard helper ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Deploy service to staging", "Deploy service to staging", 1.0)]
    [InlineData("Unrelated task", "Completely different work item", 0.0)]
    [InlineData("Review PR auth module", "Review PR auth module tests", 0.75)]
    public void JaccardSimilarity_ReturnsExpectedRange(string a, string b, double expected)
    {
        var actual = DependencyLinker.JaccardSimilarity(a, b);
        Assert.True(Math.Abs(actual - expected) < 0.15,
            $"Expected ~{expected} but got {actual:F3} for '{a}' vs '{b}'");
    }
}
