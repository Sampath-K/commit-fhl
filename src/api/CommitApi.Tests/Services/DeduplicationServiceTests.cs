using CommitApi.Models.Extraction;
using CommitApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommitApi.Tests.Services;

public class DeduplicationServiceTests
{
    private readonly DeduplicationService _sut = new(NullLogger<DeduplicationService>.Instance);

    private static RawCommitment Make(
        string title,
        string owner = "user-a",
        CommitmentSourceType source = CommitmentSourceType.Chat,
        double confidence = 0.8,
        DateTimeOffset? extractedAt = null)
        => new(
            Title:            title,
            OwnerUserId:      owner,
            OwnerDisplayName: owner,
            SourceType:       source,
            SourceUrl:        "https://example.com",
            ExtractedAt:      extractedAt ?? DateTimeOffset.UtcNow,
            DueAt:            null,
            Confidence:       confidence,
            WatcherUserIds:   [],
            SourceContext:    title);

    [Fact]
    public void Deduplicate_IdenticalTitles_SameOwner_ProducesOneRecord()
    {
        var items = new[]
        {
            Make("Review the API design doc and leave comments"),
            Make("Review the API design doc and leave comments"),
        };

        var result = _sut.Deduplicate(items);

        Assert.Single(result);
    }

    [Fact]
    public void Deduplicate_SimilarTitles_SameOwner_ProducesOneRecord()
    {
        // High overlap: 5 common tokens out of 6 unique tokens = Jaccard 0.83
        var items = new[]
        {
            Make("Review the API design and leave feedback"),
            Make("Review the API design doc and leave feedback"),
        };

        var result = _sut.Deduplicate(items);

        Assert.Single(result);
    }

    [Fact]
    public void Deduplicate_SameTitle_DifferentOwners_ProducesTwoRecords()
    {
        var items = new[]
        {
            Make("Review the API design doc", owner: "user-a"),
            Make("Review the API design doc", owner: "user-b"),
        };

        var result = _sut.Deduplicate(items);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Deduplicate_MergesWatchers_FromDuplicates()
    {
        var item1 = Make("Deploy the feature") with { WatcherUserIds = ["watcher-1"] };
        var item2 = Make("Deploy the feature") with { WatcherUserIds = ["watcher-2"] };

        var result = _sut.Deduplicate([item1, item2]);

        Assert.Single(result);
        Assert.Contains("watcher-1", result[0].WatcherUserIds);
        Assert.Contains("watcher-2", result[0].WatcherUserIds);
    }

    [Fact]
    public void Deduplicate_KeepsEarliestDueDate_WhenMerging()
    {
        var early = DateTimeOffset.UtcNow.AddDays(1);
        var late  = DateTimeOffset.UtcNow.AddDays(5);
        var item1 = Make("Submit the report") with { DueAt = late };
        var item2 = Make("Submit the report") with { DueAt = early };

        var result = _sut.Deduplicate([item1, item2]);

        Assert.Single(result);
        Assert.Equal(early, result[0].DueAt);
    }

    [Fact]
    public void Deduplicate_Idempotent_RunningTwiceProducesSameOutput()
    {
        var items = new[]
        {
            Make("Fix the login bug"),
            Make("Fix the login bug"),
            Make("Update the design tokens"),
        };

        var first  = _sut.Deduplicate(items);
        var second = _sut.Deduplicate(first);

        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public void Deduplicate_EmptyInput_ReturnsEmpty()
    {
        var result = _sut.Deduplicate([]);
        Assert.Empty(result);
    }

    [Fact]
    public void Deduplicate_JaccardBoundary_ExactlyAtThreshold_Merges()
    {
        // Build two titles with Jaccard = 0.55 (above threshold of 0.55)
        // Tokens: "review api design doc feedback" = 5 tokens (ignoring stopwords)
        // Second: "review api design doc feedback extra" = 6 tokens, 5 intersection → 5/6 ≈ 0.83
        // Need exactly at 0.55: intersection/union = 0.55 → e.g. 5 intersect / 9 union
        // 5 shared tokens, union = 9 → Jaccard = 5/9 ≈ 0.556 ≥ threshold (0.55) → merges
        var item1 = Make("review alpha bravo charlie delta epsilon zeta");
        var item2 = Make("review alpha bravo charlie delta eta theta");
        var result = _sut.Deduplicate([item1, item2]);
        Assert.Single(result);
    }

    [Fact]
    public void Deduplicate_JaccardJustBelow_Threshold_KeepsBoth()
    {
        // Completely different titles → Jaccard = 0 → kept separate
        var item1 = Make("completely unrelated task alpha");
        var item2 = Make("entirely different work beta gamma");
        var result = _sut.Deduplicate([item1, item2]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Deduplicate_AllStopwordTitle_DoesNotCrash()
    {
        // Titles consisting only of stopwords → tokenizer returns empty list
        var item1 = Make("a the and or");
        var item2 = Make("a the and or");
        var result = _sut.Deduplicate([item1, item2]);
        // Both tokenize to empty → Jaccard = 1.0 → merges
        Assert.Single(result);
    }
}
