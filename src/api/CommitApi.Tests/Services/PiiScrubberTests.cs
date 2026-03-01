using CommitApi.Config;
using FluentAssertions;
using Xunit;

namespace CommitApi.Tests.Services;

/// <summary>
/// Unit tests for PiiScrubber.
/// Verifies that telemetry payloads are correctly scrubbed before emission.
/// </summary>
public class PiiScrubberTests
{
    [Fact]
    public void Scrub_RemovesPiiFields()
    {
        // Arrange
        var props = new Dictionary<string, string>
        {
            ["rawText"] = "I will finish the report by Friday",
            ["title"] = "Finish the report",
            ["featureArea"] = "commit-pane"
        };

        // Act
        PiiScrubber.Scrub(props);

        // Assert
        props.Should().NotContainKey("rawText");
        props.Should().NotContainKey("title");
        props.Should().ContainKey("featureArea"); // non-PII field preserved
    }

    [Fact]
    public void Scrub_HashesUserIdFields()
    {
        // Arrange
        var originalUserId = "00000000-aaaa-bbbb-cccc-000000000001";
        var props = new Dictionary<string, string>
        {
            ["userId"] = originalUserId,
            ["kpiType"] = "commitment-extracted"
        };

        // Act
        PiiScrubber.Scrub(props);

        // Assert
        props["userId"].Should().NotBe(originalUserId, because: "userId must be hashed");
        props["userId"].Should().HaveLength(16, because: "HashValue returns 16-char hex prefix");
        props["kpiType"].Should().Be("commitment-extracted"); // non-PII preserved
    }

    [Fact]
    public void Scrub_TruncatesLongStrings()
    {
        // Arrange
        var longValue = new string('x', 300);
        var props = new Dictionary<string, string>
        {
            ["someField"] = longValue
        };

        // Act
        PiiScrubber.Scrub(props);

        // Assert
        props["someField"].Should().HaveLength(200);
        props["someField"].Should().EndWith("...");
    }

    [Fact]
    public void HashValue_SameInput_ReturnsSameHash()
    {
        // Deterministic — same input always produces same hash (required for join-ability in telemetry)
        var hash1 = PiiScrubber.HashValue("user-oid-123");
        var hash2 = PiiScrubber.HashValue("user-oid-123");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashValue_DifferentInputs_ReturnsDifferentHashes()
    {
        var hash1 = PiiScrubber.HashValue("user-a");
        var hash2 = PiiScrubber.HashValue("user-b");

        hash1.Should().NotBe(hash2);
    }
}
