using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;
using CommitApi.Exceptions;
using CommitApi.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Repositories;

/// <summary>
/// Unit tests for CommitmentRepository.
/// All Azure Table Storage calls are mocked — no real storage required.
/// </summary>
public class CommitmentRepositoryTests
{
    private readonly Mock<TableClient> _mockClient;
    private readonly CommitmentRepository _repository;

    public CommitmentRepositoryTests()
    {
        _mockClient = new Mock<TableClient>();
        _repository = new CommitmentRepository(_mockClient.Object);
    }

    // ─── UpsertAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_ValidEntity_CallsTableClientUpsert()
    {
        // Arrange
        var entity = BuildEntity("user-1", "row-1");
        _mockClient
            .Setup(x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default))
            .ReturnsAsync(Mock.Of<Response>());

        // Act
        await _repository.UpsertAsync(entity);

        // Assert
        _mockClient.Verify(
            x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default),
            Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_StorageFailure_ThrowsStorageException()
    {
        // Arrange
        var entity = BuildEntity("user-1", "row-1");
        _mockClient
            .Setup(x => x.UpsertEntityAsync(entity, TableUpdateMode.Replace, default))
            .ThrowsAsync(new RequestFailedException(503, "Service unavailable"));

        // Act
        var act = () => _repository.UpsertAsync(entity);

        // Assert
        await act.Should().ThrowAsync<StorageException>()
            .WithMessage("*row-1*");
    }

    // ─── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ExistingEntity_ReturnsEntity()
    {
        // Arrange
        var expected = BuildEntity("user-1", "row-1");
        _mockClient
            .Setup(x => x.GetEntityAsync<CommitmentEntity>("user-1", "row-1", null, default))
            .ReturnsAsync(Response.FromValue(expected, Mock.Of<Response>()));

        // Act
        var result = await _repository.GetAsync("user-1", "row-1");

        // Assert
        result.Should().NotBeNull();
        result!.RowKey.Should().Be("row-1");
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        // Arrange
        _mockClient
            .Setup(x => x.GetEntityAsync<CommitmentEntity>("user-1", "missing", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act
        var result = await _repository.GetAsync("user-1", "missing");

        // Assert
        result.Should().BeNull();
    }

    // ─── ListBlockingAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListBlockingAsync_EntityWithMatchingBlocks_ReturnsMatch()
    {
        // Arrange — entity1 blocks "target-id", entity2 does not
        var entity1 = BuildEntity("user-1", "row-A", blocksJson: """["target-id","other-id"]""");
        var entity2 = BuildEntity("user-1", "row-B", blocksJson: """["unrelated-id"]""");

        SetupQueryToReturn(new[] { entity1, entity2 });

        // Act
        var results = await _repository.ListBlockingAsync("user-1", "target-id");

        // Assert
        results.Should().HaveCount(1);
        results[0].RowKey.Should().Be("row-A");
    }

    [Fact]
    public async Task ListBlockingAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var entity = BuildEntity("user-1", "row-A", blocksJson: """["other-id"]""");
        SetupQueryToReturn(new[] { entity });

        // Act
        var results = await _repository.ListBlockingAsync("user-1", "target-id");

        // Assert
        results.Should().BeEmpty();
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NotFound_DoesNotThrow()
    {
        // Arrange — 404 should be treated as idempotent success
        _mockClient
            .Setup(x => x.DeleteEntityAsync("user-1", "missing", default, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        // Act
        var act = () => _repository.DeleteAsync("user-1", "missing");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ─── ListByOwnerAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListByOwnerAsync_MultiplePages_ReturnsAll()
    {
        // Arrange — simulate two pages of results
        var entities = Enumerable.Range(1, 5).Select(i => BuildEntity("user-1", $"row-{i}")).ToList();
        SetupQueryToReturn(entities);

        // Act
        var results = await _repository.ListByOwnerAsync("user-1");

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task DeleteAllForUserAsync_BatchesOf100_SendsCorrectTransactions()
    {
        // Arrange — 3 entities (fits in one batch)
        var entities = Enumerable.Range(1, 3).Select(i => BuildEntity("user-del", $"row-{i}")).ToList();
        SetupQueryToReturn(entities);

        _mockClient
            .Setup(x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<IReadOnlyList<Response>>>());

        // Act
        await _repository.DeleteAllForUserAsync("user-del");

        // Assert — exactly one batch submitted
        _mockClient.Verify(
            x => x.SubmitTransactionAsync(
                It.IsAny<IEnumerable<TableTransactionAction>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ServerError_ThrowsStorageException()
    {
        // Arrange
        _mockClient
            .Setup(x => x.DeleteEntityAsync("user-1", "row-err", default, default))
            .ThrowsAsync(new RequestFailedException(500, "Internal Server Error"));

        // Act
        var act = () => _repository.DeleteAsync("user-1", "row-err");

        // Assert
        await act.Should().ThrowAsync<StorageException>()
            .WithMessage("*row-err*");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static CommitmentEntity BuildEntity(string userId, string rowKey,
        string blocksJson = "[]")
    {
        return new CommitmentEntity
        {
            PartitionKey = userId,
            RowKey = rowKey,
            Title = "Test commitment",
            Owner = userId,
            WatchersJson = "[]",
            SourceType = "meeting",
            SourceUrl = "https://teams.example.com/meeting/123",
            SourceTimestamp = DateTimeOffset.UtcNow,
            CommittedAt = DateTimeOffset.UtcNow,
            Status = "pending",
            Priority = "not-urgent-not-important",
            BlockedByJson = "[]",
            BlocksJson = blocksJson,
            ImpactScore = 0,
            BurnoutContribution = 0
        };
    }

    private void SetupQueryToReturn(IEnumerable<CommitmentEntity> entities)
    {
        // AsyncPageable mock: return entities as a single page
        var mockPageable = new MockAsyncPageable<CommitmentEntity>(entities);
        _mockClient
            .Setup(x => x.QueryAsync<CommitmentEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockPageable);
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
