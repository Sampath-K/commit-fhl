using Azure;
using Azure.Data.Tables;
using CommitApi.Entities;
using CommitApi.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Repositories;

public class SyncStateRepositoryTests
{
    private readonly Mock<TableClient> _mockTable = new();
    private readonly SyncStateRepository _sut;

    public SyncStateRepositoryTests()
    {
        _sut = new SyncStateRepository(_mockTable.Object);
    }

    [Fact]
    public async Task GetDeltaTokenAsync_NoEntry_ReturnsNull()
    {
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("user1", "email", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var result = await _sut.GetDeltaTokenAsync("user1", "email");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAndGetDeltaToken_RoundTrips()
    {
        var entity = new SyncStateEntity
        {
            PartitionKey = "user1",
            RowKey       = "email",
            DeltaToken   = "delta-abc"
        };

        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("user1", "email", null, default))
            .ReturnsAsync(Response.FromValue(entity, Mock.Of<Response>()));

        var result = await _sut.GetDeltaTokenAsync("user1", "email");

        result.Should().Be("delta-abc");
    }

    [Fact]
    public async Task SaveDeltaTokenAsync_Upserts()
    {
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("user1", "planner", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));
        _mockTable
            .Setup(x => x.UpsertEntityAsync(It.IsAny<SyncStateEntity>(), TableUpdateMode.Replace, default))
            .ReturnsAsync(Mock.Of<Response>());

        await _sut.SaveDeltaTokenAsync("user1", "planner", "delta-xyz");

        _mockTable.Verify(
            x => x.UpsertEntityAsync(
                It.Is<SyncStateEntity>(e => e.DeltaToken == "delta-xyz"),
                TableUpdateMode.Replace, default),
            Times.Once);
    }

    [Fact]
    public async Task GetWatermarkAsync_NoEntry_ReturnsNull()
    {
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("user2", "chat", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var result = await _sut.GetWatermarkAsync("user2", "chat");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveWatermarkAsync_Upserts()
    {
        var now = DateTimeOffset.UtcNow;
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("user2", "chat", null, default))
            .ThrowsAsync(new RequestFailedException(404, "Not found"));
        _mockTable
            .Setup(x => x.UpsertEntityAsync(It.IsAny<SyncStateEntity>(), TableUpdateMode.Replace, default))
            .ReturnsAsync(Mock.Of<Response>());

        await _sut.SaveWatermarkAsync("user2", "chat", now);

        _mockTable.Verify(
            x => x.UpsertEntityAsync(
                It.Is<SyncStateEntity>(e => e.Watermark == now),
                TableUpdateMode.Replace, default),
            Times.Once);
    }

    [Fact]
    public async Task DifferentExtractors_AreIsolated()
    {
        var entityEmail = new SyncStateEntity { PartitionKey = "u1", RowKey = "email", DeltaToken = "email-token" };
        var entityPlanner = new SyncStateEntity { PartitionKey = "u1", RowKey = "planner", DeltaToken = "planner-token" };

        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("u1", "email", null, default))
            .ReturnsAsync(Response.FromValue(entityEmail, Mock.Of<Response>()));
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("u1", "planner", null, default))
            .ReturnsAsync(Response.FromValue(entityPlanner, Mock.Of<Response>()));

        var emailToken   = await _sut.GetDeltaTokenAsync("u1", "email");
        var plannerToken = await _sut.GetDeltaTokenAsync("u1", "planner");

        emailToken.Should().Be("email-token");
        plannerToken.Should().Be("planner-token");
    }

    [Fact]
    public async Task DifferentUsers_AreIsolated()
    {
        var entityA = new SyncStateEntity { PartitionKey = "userA", RowKey = "email", DeltaToken = "token-a" };
        var entityB = new SyncStateEntity { PartitionKey = "userB", RowKey = "email", DeltaToken = "token-b" };

        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("userA", "email", null, default))
            .ReturnsAsync(Response.FromValue(entityA, Mock.Of<Response>()));
        _mockTable
            .Setup(x => x.GetEntityAsync<SyncStateEntity>("userB", "email", null, default))
            .ReturnsAsync(Response.FromValue(entityB, Mock.Of<Response>()));

        var tokenA = await _sut.GetDeltaTokenAsync("userA", "email");
        var tokenB = await _sut.GetDeltaTokenAsync("userB", "email");

        tokenA.Should().Be("token-a");
        tokenB.Should().Be("token-b");
    }
}
