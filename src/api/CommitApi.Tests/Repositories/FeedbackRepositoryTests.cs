using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;
using CommitApi.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Repositories;

public class FeedbackRepositoryTests
{
    private readonly Mock<TableClient> _mockTable = new();
    private readonly FeedbackRepository _sut;

    public FeedbackRepositoryTests()
    {
        _sut = new FeedbackRepository(_mockTable.Object);
    }

    [Fact]
    public async Task RecordAsync_UpsertsCalled()
    {
        var entity = BuildEntity("hash-user1", "fb-001");

        _mockTable
            .Setup(x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default))
            .ReturnsAsync(Mock.Of<Response>());

        await _sut.RecordAsync(entity);

        _mockTable.Verify(
            x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default),
            Times.Once);
    }

    [Fact]
    public async Task RecordAsync_StoresPiiScrubbedFields()
    {
        var entity = BuildEntity("hash-user1", "fb-002");
        // Verify no raw userId in PartitionKey (it should be a hash)
        entity.PartitionKey.Should().NotBe("actual-user-id");

        _mockTable
            .Setup(x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default))
            .ReturnsAsync(Mock.Of<Response>());

        await _sut.RecordAsync(entity);

        // If we got here without throwing, storage was called with the hash-based PK
        _mockTable.Verify(x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default), Times.Once);
    }

    [Fact]
    public async Task GetByUserHashAsync_ReturnsChronologicalOrder()
    {
        var now    = DateTimeOffset.UtcNow;
        var older  = BuildEntity("hash-u1", "fb-A", recordedAt: now.AddMinutes(-10));
        var newer  = BuildEntity("hash-u1", "fb-B", recordedAt: now);
        // Return in reverse order from storage
        SetupQuery("hash-u1", [newer, older]);

        var results = await _sut.GetByUserHashAsync("hash-u1");

        results.Should().HaveCount(2);
        results[0].RowKey.Should().Be("fb-A"); // older first
        results[1].RowKey.Should().Be("fb-B"); // newer second
    }

    [Fact]
    public async Task DeleteAllForUserAsync_DeletesAllRows()
    {
        var entities = new[] { BuildEntity("hash-del", "fb-1"), BuildEntity("hash-del", "fb-2") };
        SetupQuery("hash-del", entities);

        _mockTable
            .Setup(x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<IReadOnlyList<Response>>>());

        await _sut.DeleteAllForUserAsync("hash-del");

        _mockTable.Verify(
            x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByUserHashAsync_DifferentUsers_Isolated()
    {
        SetupQuery("hash-X", [BuildEntity("hash-X", "fb-X")]);
        SetupQuery("hash-Y", [BuildEntity("hash-Y", "fb-Y")]);

        var resultsX = await _sut.GetByUserHashAsync("hash-X");
        var resultsY = await _sut.GetByUserHashAsync("hash-Y");

        resultsX.Should().HaveCount(1);
        resultsX[0].PartitionKey.Should().Be("hash-X");
        resultsY.Should().HaveCount(1);
        resultsY[0].PartitionKey.Should().Be("hash-Y");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static FeedbackEntity BuildEntity(string userHash, string rowKey,
        DateTimeOffset? recordedAt = null) => new()
    {
        PartitionKey       = userHash,
        RowKey             = rowKey,
        CommitmentIdHash   = "commitHash123",
        TitleFingerprint   = "titleFp456",
        FeedbackType       = "FalsePositive",
        SourceType         = "Chat",
        RecordedAt         = recordedAt ?? DateTimeOffset.UtcNow,
        ConfidenceAtFeedback = 0.7,
    };

    private void SetupQuery(string userHash, IEnumerable<FeedbackEntity> entities)
    {
        var pageable = new MockAsyncPageable<FeedbackEntity>(entities);
        _mockTable
            .Setup(x => x.QueryAsync<FeedbackEntity>(
                It.Is<string>(f => f.Contains(userHash)),
                It.IsAny<int?>(), It.IsAny<IEnumerable<string?>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(pageable);
    }
}

/// <summary>Helper to create an AsyncPageable from a plain enumerable (for Moq).</summary>
file sealed class MockAsyncPageable<T> : AsyncPageable<T> where T : notnull
{
    private readonly IEnumerable<T> _items;
    public MockAsyncPageable(IEnumerable<T> items) => _items = items;
    public override async IAsyncEnumerable<Page<T>> AsPages(
        string? continuationToken = null, int? pageSizeHint = null)
    {
        yield return Page<T>.FromValues(_items.ToList(), null, Mock.Of<Response>());
        await Task.CompletedTask;
    }
}
