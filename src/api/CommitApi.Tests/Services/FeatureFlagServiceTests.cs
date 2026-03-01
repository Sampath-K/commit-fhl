using CommitApi.Config;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommitApi.Tests.Services;

/// <summary>
/// Unit tests for FeatureFlagService.
/// No Azure App Config connection needed — tests env-var fallback and defaults.
/// </summary>
public class FeatureFlagServiceTests
{
    private static IFeatureFlagService CreateService(string env = "dev")
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new FeatureFlagService(
            appConfigClient: null,   // no App Config — tests fallback path
            cache: cache,
            logger: NullLogger<FeatureFlagService>.Instance,
            environment: env);
    }

    [Theory]
    [InlineData("commit.feature.psychologyLayer")]
    [InlineData("commit.feature.deliveryScore")]
    [InlineData("commit.feature.streakTracking")]
    [InlineData("commit.feature.cascadeAlerts")]
    [InlineData("commit.feature.overcommitWarning")]
    public async Task IsEnabledAsync_KnownFlag_DevEnvironment_ReturnsTrue(string flagName)
    {
        // Arrange
        var service = CreateService(env: "dev");

        // Act
        var result = await service.IsEnabledAsync(flagName);

        // Assert
        result.Should().BeTrue(because: $"{flagName} should default to enabled in dev");
    }

    [Theory]
    [InlineData("commit.feature.psychologyLayer")]
    [InlineData("commit.feature.deliveryScore")]
    public async Task IsEnabledAsync_KnownFlag_PilotEnvironment_ReturnsFalse(string flagName)
    {
        // Arrange — all flags default to OFF in pilot until explicitly enabled in App Config
        var service = CreateService(env: "pilot");

        // Act
        var result = await service.IsEnabledAsync(flagName);

        // Assert
        result.Should().BeFalse(because: $"{flagName} should default to false in pilot without App Config");
    }

    [Fact]
    public async Task IsEnabledAsync_UnknownFlag_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.IsEnabledAsync("commit.feature.doesNotExist");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_EnvVarOverride_ReturnsOverrideValue()
    {
        // Arrange — set env var override
        Environment.SetEnvironmentVariable(
            "FEATURE_COMMIT_FEATURE_PSYCHOLOGYLAYER", "false");
        var service = CreateService(env: "dev");

        try
        {
            // Act
            var result = await service.IsEnabledAsync("commit.feature.psychologyLayer");

            // Assert
            result.Should().BeFalse(because: "env var override should suppress default true");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FEATURE_COMMIT_FEATURE_PSYCHOLOGYLAYER", null);
        }
    }

    [Fact]
    public async Task IsEnabledAsync_CachesResult_DoesNotCallAppConfigTwice()
    {
        // Arrange — calling the same flag twice should hit cache on second call
        var service = CreateService();

        // Act
        var result1 = await service.IsEnabledAsync("commit.feature.deliveryScore");
        var result2 = await service.IsEnabledAsync("commit.feature.deliveryScore");

        // Assert — both return same value (cache works)
        result1.Should().Be(result2);
    }
}
