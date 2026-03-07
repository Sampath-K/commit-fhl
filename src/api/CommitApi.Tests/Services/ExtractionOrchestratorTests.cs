using CommitApi.Auth;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Extractors;
using CommitApi.Models.Extraction;
using CommitApi.Models.Feedback;
using CommitApi.Repositories;
using CommitApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CommitApi.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ExtractionOrchestrator"/>.
/// All external dependencies are mocked via Moq so no network or storage calls are made.
/// </summary>
public class ExtractionOrchestratorTests
{
    // ─── Mocks ────────────────────────────────────────────────────────────────

    private readonly Mock<ITranscriptExtractor>  _transcripts    = new();
    private readonly Mock<IChatExtractor>        _chats          = new();
    private readonly Mock<IEmailExtractor>       _emails         = new();
    private readonly Mock<IAdoExtractor>         _ado            = new();
    private readonly Mock<IDriveExtractor>       _drive          = new();
    private readonly Mock<IPlannerExtractor>     _planner        = new();
    private readonly Mock<INlpPipeline>          _nlp            = new();
    private readonly Mock<IDeduplicationService> _dedup          = new();
    private readonly Mock<IEisenhowerScorer>     _scorer         = new();
    private readonly Mock<ICommitmentRepository> _repo           = new();
    private readonly Mock<IAppInsightsClient>    _insights       = new();
    private readonly Mock<IGraphClientFactory>   _graphFactory   = new();
    private readonly Mock<ISyncStateRepository>  _syncState      = new();
    private readonly Mock<IFeedbackRepository>   _feedbackRepo   = new();
    private readonly Mock<ISignalProfileService> _signalProfile  = new();

    // CommitmentEventBus is sealed with no interface — instantiate directly.
    private readonly CommitmentEventBus _eventBus =
        new CommitmentEventBus();

    private ExtractionOrchestrator BuildSut()
        => new(
            _transcripts.Object,
            _chats.Object,
            _emails.Object,
            _ado.Object,
            _drive.Object,
            _planner.Object,
            _nlp.Object,
            _dedup.Object,
            _scorer.Object,
            _repo.Object,
            _insights.Object,
            _eventBus,
            _graphFactory.Object,
            _syncState.Object,
            _feedbackRepo.Object,
            _signalProfile.Object,
            NullLogger<ExtractionOrchestrator>.Instance);

    public ExtractionOrchestratorTests()
    {
        // ── Default extractor stubs (return empty) ────────────────────────────
        _transcripts.Setup(x => x.GetChunksAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TranscriptChunk>());

        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        _emails.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        _ado.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        _drive.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        _planner.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        // ── NLP returns empty by default ──────────────────────────────────────
        _nlp.Setup(x => x.ExtractFromChunksAsync(
                It.IsAny<IEnumerable<TranscriptChunk>>(),
                It.IsAny<UserSignalProfile?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawCommitment>());

        // ── Dedup passes through its input ────────────────────────────────────
        _dedup.Setup(x => x.Deduplicate(It.IsAny<IEnumerable<RawCommitment>>()))
            .Returns<IEnumerable<RawCommitment>>(items => items.ToList());

        // ── Scorer returns "schedule" ─────────────────────────────────────────
        _scorer.Setup(x => x.Score(
                It.IsAny<RawCommitment>(), It.IsAny<UserSignalProfile?>()))
            .Returns("schedule");

        // ── Profile service returns Default ───────────────────────────────────
        _signalProfile.Setup(x => x.GetProfileAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserSignalProfile.Default);

        // ── Graph factory returns an OBO token ────────────────────────────────
        _graphFactory.Setup(x => x.GetOboTokenAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("obo-token");

        // ── Repo upsert is a no-op by default ────────────────────────────────
        _repo.Setup(x => x.UpsertAsync(
                It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static RawCommitment MakeCommitment(
        string title = "Test commitment",
        ItemKind kind = ItemKind.Commitment)
        => new(
            Title:            title,
            OwnerUserId:      "user1",
            OwnerDisplayName: "Me",
            SourceType:       CommitmentSourceType.Chat,
            SourceUrl:        "https://example.com",
            ExtractedAt:      DateTimeOffset.UtcNow,
            DueAt:            null,
            Confidence:       0.8,
            WatcherUserIds:   [],
            SourceContext:    "context",
            SourceMetadata:   null,
            ProjectContext:   null,
            ArtifactName:     null,
            ItemKind:         kind);

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAndStoreAsync_AllExtractorsReturnItems_ReturnsCorrectCount()
    {
        // Arrange — each extractor returns 2 items
        var twoItems = new[] { MakeCommitment("Item A"), MakeCommitment("Item B") };

        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        _emails.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        _ado.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        _drive.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        _planner.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        _nlp.Setup(x => x.ExtractFromChunksAsync(
                It.IsAny<IEnumerable<TranscriptChunk>>(),
                It.IsAny<UserSignalProfile?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(twoItems);

        // 5 extractors x 2 + NLP 2 = 12; dedup returns all
        var sut = BuildSut();

        // Act
        var count = await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert
        count.Should().Be(12);
        _repo.Verify(
            x => x.UpsertAsync(It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()),
            Times.Exactly(12));
    }

    [Fact]
    public async Task ExtractAndStoreAsync_OboFails_FallsBackToSsoToken()
    {
        // Arrange — OBO exchange throws
        _graphFactory.Setup(x => x.GetOboTokenAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("OBO failed"));

        // ADO extractor should still be called with the original bearer token
        _ado.Setup(x => x.ExtractAsync(
                "user1", "sso-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeCommitment("ADO task") });

        var sut = BuildSut();

        // Act
        var count = await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert — extraction still ran; ADO item was stored
        count.Should().Be(1);
        _ado.Verify(
            x => x.ExtractAsync("user1", "sso-token", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_AllExtractorsReturnEmpty_Returns0()
    {
        // All defaults return empty; dedup returns empty; NLP returns empty.
        var sut = BuildSut();

        var count = await sut.ExtractAndStoreAsync("user1", "sso-token");

        count.Should().Be(0);
        _repo.Verify(
            x => x.UpsertAsync(It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_CompletionItem_StoredWithStatusDone()
    {
        // Arrange — planner returns a Completion item
        var completionItem = MakeCommitment("Ship the milestone", ItemKind.Completion);

        _planner.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { completionItem });

        CommitmentEntity? captured = null;
        _repo.Setup(x => x.UpsertAsync(It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CommitmentEntity, CancellationToken>((entity, _) => captured = entity)
            .Returns(Task.CompletedTask);

        var sut = BuildSut();

        // Act
        await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert
        captured.Should().NotBeNull();
        captured!.Status.Should().Be("done");
        captured.ItemKind.Should().Be("completion");
    }

    [Fact]
    public async Task ExtractAndStoreAsync_EventBusPublished()
    {
        // Arrange — one item so upserted > 0 and Publish is called
        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeCommitment() });

        // Subscribe so we can observe the publish
        var reader = _eventBus.Subscribe("user1");

        var sut = BuildSut();

        // Act
        await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert — the event bus should have written 1 to the channel
        reader.TryRead(out var published).Should().BeTrue();
        published.Should().Be(1);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_ProfileSuppressesMatchingFingerprint()
    {
        // Arrange — profile suppresses the fingerprint of "fix the bug"
        var titleToSuppress  = "fix the bug";
        var suppressedFp     = ExtractionOrchestrator.ComputeTitleFingerprint(titleToSuppress);
        var suppressedProfile = new UserSignalProfile(
            ConfidenceAdjustment:   0.0,
            SuppressedFingerprints: new HashSet<string> { suppressedFp },
            NlpNegativeExamples:    [],
            NlpPositiveExamples:    []);

        _signalProfile.Setup(x => x.GetProfileAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suppressedProfile);

        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeCommitment(titleToSuppress) });

        var sut = BuildSut();

        // Act
        var count = await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert — suppressed item must NOT be upserted
        count.Should().Be(0);
        _repo.Verify(
            x => x.UpsertAsync(It.IsAny<CommitmentEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_ProfilePassedToNlpPipeline()
    {
        // Arrange — set a custom profile so we can verify it flows through
        var customProfile = new UserSignalProfile(
            ConfidenceAdjustment:   0.05,
            SuppressedFingerprints: new HashSet<string>(),
            NlpNegativeExamples:    ["will try"],
            NlpPositiveExamples:    []);

        _signalProfile.Setup(x => x.GetProfileAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customProfile);

        var sut = BuildSut();

        // Act
        await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert — NLP must have been called with the exact profile instance
        _nlp.Verify(
            x => x.ExtractFromChunksAsync(
                It.IsAny<IEnumerable<TranscriptChunk>>(),
                customProfile,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_ProfilePassedToScorer()
    {
        // Arrange — return one item so scorer is exercised
        var customProfile = new UserSignalProfile(
            ConfidenceAdjustment:   0.1,
            SuppressedFingerprints: new HashSet<string>(),
            NlpNegativeExamples:    [],
            NlpPositiveExamples:    []);

        _signalProfile.Setup(x => x.GetProfileAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customProfile);

        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeCommitment() });

        var sut = BuildSut();

        // Act
        await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert — scorer must receive the exact profile
        _scorer.Verify(
            x => x.Score(It.IsAny<RawCommitment>(), customProfile),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAndStoreAsync_TelemetryTrackedWithSuppressedCount()
    {
        // Arrange — 2 items, both suppressed by profile
        const string title1 = "review the design doc";
        const string title2 = "update the changelog";

        var suppressedProfile = new UserSignalProfile(
            ConfidenceAdjustment:   0.0,
            SuppressedFingerprints: new HashSet<string>
            {
                ExtractionOrchestrator.ComputeTitleFingerprint(title1),
                ExtractionOrchestrator.ComputeTitleFingerprint(title2),
            },
            NlpNegativeExamples: [],
            NlpPositiveExamples: []);

        _signalProfile.Setup(x => x.GetProfileAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suppressedProfile);

        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeCommitment(title1),
                MakeCommitment(title2),
            });

        IDictionary<string, string>? capturedProps = null;
        _insights.Setup(x => x.TrackUserAction(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>?>()))
            .Callback<string, string, string, IDictionary<string, string>?>(
                (_, _, _, props) => capturedProps = props);

        var sut = BuildSut();

        // Act
        await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert
        capturedProps.Should().NotBeNull();
        capturedProps!["suppressed"].Should().Be("2");
    }

    [Fact]
    public async Task ExtractAndStoreAsync_ReturnsUpsertedCount()
    {
        // Arrange — 3 items after dedup
        _chats.Setup(x => x.ExtractAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeCommitment("Task one"),
                MakeCommitment("Task two"),
                MakeCommitment("Task three"),
            });

        var sut = BuildSut();

        // Act
        var result = await sut.ExtractAndStoreAsync("user1", "sso-token");

        // Assert
        result.Should().Be(3);
    }
}
