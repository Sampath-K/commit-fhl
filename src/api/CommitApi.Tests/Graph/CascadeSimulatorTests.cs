using CommitApi.Entities;
using CommitApi.Graph;
using CommitApi.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Graph;

public class CascadeSimulatorTests
{
    private static CommitmentEntity MakeEntity(
        string rowKey,
        string[] blocks,
        DateTimeOffset dueAt,
        string userId = "u1") =>
        new()
        {
            PartitionKey  = userId,
            RowKey        = rowKey,
            Title         = $"Task {rowKey}",
            Owner         = userId,
            WatchersJson  = "[]",
            BlocksJson    = System.Text.Json.JsonSerializer.Serialize(blocks),
            BlockedByJson = "[]",
            CommittedAt   = DateTimeOffset.UtcNow.AddDays(-1),
            DueAt         = dueAt,
            Status        = "pending"
        };

    private static CascadeSimulator BuildSimulator(ICommitmentRepository repo) =>
        new(repo, NullLogger<CascadeSimulator>.Instance);

    /// <summary>
    /// 5-task chain: A→B→C→D→E.
    /// A slips by 2 days, causing B, C, D to also slip (E has slack so it is unaffected).
    /// </summary>
    [Fact]
    public async Task Simulate_FiveTaskChain_PropagatesTwoDaySlip()
    {
        var now = DateTimeOffset.UtcNow.Date;

        // Chain: A blocks B, B blocks C, C blocks D, D blocks E
        // A due +1d, B due +1.5d, C due +2d, D due +2.5d, E due +5d (has slack)
        var a = MakeEntity("A", ["B"], now.AddDays(1));
        var b = MakeEntity("B", ["C"], now.AddDays(1.5));
        var c = MakeEntity("C", ["D"], now.AddDays(2));
        var d = MakeEntity("D", ["E"], now.AddDays(2.5));
        var e = MakeEntity("E", [],    now.AddDays(5));   // plenty of slack

        var all = new List<CommitmentEntity> { a, b, c, d, e };

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync(all);
        mock.Setup(r => r.GetAsync("u1", It.IsAny<string>(), default))
            .ReturnsAsync((string uid, string rowKey, CancellationToken _) =>
                all.FirstOrDefault(x => x.RowKey == rowKey));

        var sim    = BuildSimulator(mock.Object);
        var result = await sim.SimulateAsync("A", "u1", slipDays: 2);

        // Root task A must be in affected list
        Assert.Contains(result.AffectedTasks, t => t.TaskId == "A");

        // With 2-day slip, A's new ETA = +3d. B (due +1.5d) must slip.
        Assert.Contains(result.AffectedTasks, t => t.TaskId == "B");

        // At least A + 2 downstream tasks affected
        Assert.True(result.TotalTasksAffected >= 3,
            $"Expected ≥3 affected tasks but got {result.TotalTasksAffected}");

        // E has 5 days slack — with A slipping 2d, D new ETA ≤ 5d, so E should NOT slip
        var eTask = result.AffectedTasks.FirstOrDefault(t => t.TaskId == "E");
        if (eTask is not null)
            Assert.True(eTask.CumulativeSlipDays == 0,
                $"E should not slip but got {eTask.CumulativeSlipDays} days");
    }

    [Fact]
    public async Task Simulate_SingleTask_NoBlockees_AffectsOnlyRoot()
    {
        var now = DateTimeOffset.UtcNow.Date;
        var root = MakeEntity("R", [], now.AddDays(3));

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([root]);
        mock.Setup(r => r.GetAsync("u1", "R", default))
            .ReturnsAsync(root);

        var sim    = BuildSimulator(mock.Object);
        var result = await sim.SimulateAsync("R", "u1", slipDays: 1);

        Assert.Equal(1, result.TotalTasksAffected);
        Assert.Equal("R", result.AffectedTasks[0].TaskId);
        Assert.Equal(1, result.AffectedTasks[0].CumulativeSlipDays);
    }

    [Fact]
    public async Task Simulate_NewEtaBeforeDependentDue_DoesNotPropagate()
    {
        var now = DateTimeOffset.UtcNow.Date;
        // A due +1d, slips 1 day → new ETA +2d. B due +3d → new ETA of A (+2d) < B due (+3d) → no slip
        var a = MakeEntity("A", ["B"], now.AddDays(1));
        var b = MakeEntity("B", [],    now.AddDays(3));

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);
        mock.Setup(r => r.GetAsync("u1", It.IsAny<string>(), default))
            .ReturnsAsync((string uid, string rowKey, CancellationToken _) =>
                rowKey == "A" ? a : rowKey == "B" ? b : null);

        var sim    = BuildSimulator(mock.Object);
        var result = await sim.SimulateAsync("A", "u1", slipDays: 1);

        // A is affected; B is NOT because new ETA of A (2d) < due of B (3d)
        Assert.Equal(1, result.TotalTasksAffected);
        Assert.DoesNotContain(result.AffectedTasks, t => t.TaskId == "B");
    }

    [Fact]
    public async Task Simulate_CyclicalReferences_DoesNotInfiniteLoop()
    {
        var now = DateTimeOffset.UtcNow.Date;
        // Pathological: A blocks B, B blocks A (should terminate due to visited set)
        var a = MakeEntity("A", ["B"], now.AddDays(1));
        var b = MakeEntity("B", ["A"], now.AddDays(1));

        var mock = new Mock<ICommitmentRepository>();
        mock.Setup(r => r.ListByOwnerAsync("u1", null, default))
            .ReturnsAsync([a, b]);
        mock.Setup(r => r.GetAsync("u1", It.IsAny<string>(), default))
            .ReturnsAsync((string uid, string rowKey, CancellationToken _) =>
                rowKey == "A" ? a : rowKey == "B" ? b : null);

        var sim    = BuildSimulator(mock.Object);
        var result = await sim.SimulateAsync("A", "u1", slipDays: 1);

        // Must complete without hanging; at most 2 tasks (A and B)
        Assert.True(result.TotalTasksAffected <= 2);
    }
}
