using CommitApi.Models.Extraction;
using CommitApi.Models.Feedback;
using CommitApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommitApi.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NlpPipeline"/>.
///
/// Because <see cref="Azure.AI.OpenAI.AzureOpenAIClient"/> is sealed and does not
/// accept an injectable HttpClient, tests are limited to behaviours that are
/// exercised without a live HTTP connection:
///   - Early-return paths when AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_KEY are absent
///   - Profile parameter acceptance (no crash, correct early-return value)
///   - Empty / short-transcript guards
///   - ClassifyResolutionAsync length-mismatch handling
/// </summary>
public class NlpPipelineTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an NlpPipeline with no OpenAI endpoint configured (disabled mode).
    /// </summary>
    private static NlpPipeline CreateDisabled()
        => new(
            NullLogger<NlpPipeline>.Instance,
            new ConfigurationBuilder().Build());

    private static TranscriptChunk MakeChunk(string text, string meetingId = "m1")
        => new(
            SpeakerName:    "Alice",
            SpeakerUserId:  "user-alice",
            Text:           text,
            MeetingId:      meetingId,
            Timestamp:      DateTimeOffset.UtcNow,
            MeetingSubject: "Sprint review");

    private static RawCommitment MakeHeuristic(string title = "Fix the login bug")
        => new(
            Title:            title,
            OwnerUserId:      "user-a",
            OwnerDisplayName: "User A",
            SourceType:       CommitmentSourceType.Chat,
            SourceUrl:        "https://example.com",
            ExtractedAt:      DateTimeOffset.UtcNow,
            DueAt:            null,
            Confidence:       0.75,
            WatcherUserIds:   [],
            SourceContext:    title);

    // ─── ExtractFromChunksAsync — disabled mode ──────────────────────────────

    [Fact]
    public async Task ExtractFromChunksAsync_NoEndpoint_ReturnsEmptyList()
    {
        var sut    = CreateDisabled();
        var chunks = new[] { MakeChunk("Alice: I will send the report by Friday.") };

        var result = await sut.ExtractFromChunksAsync(chunks);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromChunksAsync_EmptyChunks_ReturnsEmptyList()
    {
        var sut = CreateDisabled();

        var result = await sut.ExtractFromChunksAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromChunksAsync_SmallChunk_LessThan50Chars_ReturnsEmptyList()
    {
        // The pipeline skips meetings whose transcript text is < 50 characters.
        // With OpenAI disabled the method returns [] before that check; either
        // way the result must be empty and no exception should be thrown.
        var sut    = CreateDisabled();
        var chunks = new[] { MakeChunk("Short.") }; // well under 50 chars

        var result = await sut.ExtractFromChunksAsync(chunks);

        result.Should().BeEmpty();
    }

    // ─── RefineAsync — disabled mode ─────────────────────────────────────────

    [Fact]
    public async Task RefineAsync_NoEndpoint_ReturnsHeuristic()
    {
        var sut       = CreateDisabled();
        var heuristic = MakeHeuristic("Deploy the feature flag");

        var result = await sut.RefineAsync(heuristic);

        result.Should().NotBeNull();
        result!.Title.Should().Be(heuristic.Title);
        result.Confidence.Should().Be(heuristic.Confidence);
    }

    // ─── ClassifyResolutionAsync — disabled mode / edge cases ────────────────

    [Fact]
    public async Task ClassifyResolutionAsync_NoEndpoint_ReturnsDefaults()
    {
        var sut = CreateDisabled();
        var titles  = new[] { "Send the report", "Review the PR" };
        var followUp = new[] { new string[] { "Here it is" }, Array.Empty<string>() };

        var result = await sut.ClassifyResolutionAsync(titles, followUp);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r =>
        {
            r.Resolved.Should().BeFalse();
            r.Evidence.Should().Be("OpenAI unavailable");
        });
    }

    [Fact]
    public async Task ClassifyResolutionAsync_EmptyInput_ReturnsEmpty()
    {
        var sut = CreateDisabled();

        var result = await sut.ClassifyResolutionAsync([], []);

        result.Should().BeEmpty();
    }

    // ─── Profile parameter acceptance — disabled mode ────────────────────────

    [Fact]
    public async Task ExtractFromChunksAsync_WithNullProfile_BehavesLikeDefault()
    {
        var sut    = CreateDisabled();
        var chunks = new[] { MakeChunk("Alice: I will finish the report by end of week.") };

        var result = await sut.ExtractFromChunksAsync(chunks, profile: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromChunksAsync_WithProfileAdjustment_DoesNotCrash()
    {
        // Even with a positive confidence adjustment the pipeline must not throw
        // when OpenAI is disabled — it should simply return [].
        var sut = CreateDisabled();
        var profile = new UserSignalProfile(
            ConfidenceAdjustment:   0.1,
            SuppressedFingerprints: new HashSet<string>(),
            NlpNegativeExamples:    [],
            NlpPositiveExamples:    []);
        var chunks = new[] { MakeChunk("Alice: I will fix the build pipeline this afternoon.") };

        var result = await sut.ExtractFromChunksAsync(chunks, profile);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromChunksAsync_WithProfileNegativeExamples_DoesNotCrash()
    {
        // Profiles with negative examples are passed to BuildSystemPrompt.
        // When OpenAI is disabled the method must still return [] without throwing.
        var sut = CreateDisabled();
        var profile = new UserSignalProfile(
            ConfidenceAdjustment:   0.0,
            SuppressedFingerprints: new HashSet<string>(),
            NlpNegativeExamples:    ["will try", "might do", "planning to"],
            NlpPositiveExamples:    []);
        var chunks = new[] { MakeChunk("Bob: I will try to push the changes by tomorrow.") };

        var result = await sut.ExtractFromChunksAsync(chunks, profile);

        result.Should().BeEmpty();
    }

    // ─── RefineAsync — profile parameter acceptance, disabled mode ───────────

    [Fact]
    public async Task RefineAsync_WithNullProfile_ReturnsHeuristic()
    {
        var sut       = CreateDisabled();
        var heuristic = MakeHeuristic("Update the docs");

        var result = await sut.RefineAsync(heuristic, profile: null);

        result.Should().NotBeNull();
        result!.Title.Should().Be(heuristic.Title);
    }

    [Fact]
    public async Task RefineAsync_WithProfileAdjustment_ReturnsHeuristic()
    {
        var sut = CreateDisabled();
        var profile = new UserSignalProfile(
            ConfidenceAdjustment:   0.1,
            SuppressedFingerprints: new HashSet<string>(),
            NlpNegativeExamples:    [],
            NlpPositiveExamples:    []);
        var heuristic = MakeHeuristic("Write the spec");

        var result = await sut.RefineAsync(heuristic, profile);

        result.Should().NotBeNull();
        result!.Title.Should().Be(heuristic.Title);
    }

    // ─── ClassifyResolutionAsync — mismatched list lengths ───────────────────

    [Fact]
    public async Task ClassifyResolutionAsync_MismatchedLengths_ReturnsDefaults()
    {
        // 3 commitment titles but only 2 follow-up arrays.
        // The pipeline must not throw; it returns a default per commitment title.
        var sut     = CreateDisabled();
        var titles  = new[] { "Task A", "Task B", "Task C" };
        var followUp = new[]
        {
            new[] { "Done — sent the link" },
            Array.Empty<string>(),
        };

        var result = await sut.ClassifyResolutionAsync(titles, followUp);

        // Disabled pipeline returns one default entry per title
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.Resolved.Should().BeFalse());
    }
}
