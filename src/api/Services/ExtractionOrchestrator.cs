using CommitApi.Auth;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Extractors;
using CommitApi.Models.Extraction;
using CommitApi.Models.Feedback;
using CommitApi.Repositories;
using CommitApi.Services;

namespace CommitApi.Services;

/// <summary>
/// Orchestrates a full extraction run for a single user:
///   1. Runs all 6 extractors concurrently (transcript, chat, email, ADO, Drive, Planner)
///   2. Runs NLP pipeline on transcript chunks
///   3. Deduplicates across all sources
///   4. Scores priority (Eisenhower)
///   5. Persists to Azure Table Storage
///
/// This is the shared extraction logic used by:
///   - POST /api/v1/extract  (user-initiated, with fresh token)
///   - ExtractionPollingService (5-min background polling)
///   - WebhookHandler (webhook-triggered on Teams/email/drive notifications)
/// </summary>
public interface IExtractionOrchestrator
{
    /// <summary>
    /// Runs a full extraction run for the given user using the provided OBO token.
    /// </summary>
    /// <param name="userId">AAD Object ID of the user to extract commitments for.</param>
    /// <param name="bearerToken">Valid OBO Bearer token for the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of new/updated commitments persisted.</returns>
    Task<int> ExtractAndStoreAsync(string userId, string bearerToken, CancellationToken ct = default);
}

public sealed class ExtractionOrchestrator : IExtractionOrchestrator
{
    private readonly ITranscriptExtractor    _transcripts;
    private readonly IChatExtractor          _chats;
    private readonly IEmailExtractor         _emails;
    private readonly IAdoExtractor           _ado;
    private readonly IDriveExtractor         _drive;
    private readonly IPlannerExtractor       _planner;
    private readonly INlpPipeline            _nlp;
    private readonly IDeduplicationService   _dedup;
    private readonly IEisenhowerScorer       _scorer;
    private readonly ICommitmentRepository   _repo;
    private readonly IAppInsightsClient      _insights;
    private readonly CommitmentEventBus      _eventBus;
    private readonly IGraphClientFactory     _graphFactory;
    private readonly ISyncStateRepository    _syncState;
    private readonly IFeedbackRepository     _feedbackRepo;
    private readonly ISignalProfileService   _signalProfile;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        ITranscriptExtractor    transcripts,
        IChatExtractor          chats,
        IEmailExtractor         emails,
        IAdoExtractor           ado,
        IDriveExtractor         drive,
        IPlannerExtractor       planner,
        INlpPipeline            nlp,
        IDeduplicationService   dedup,
        IEisenhowerScorer       scorer,
        ICommitmentRepository   repo,
        IAppInsightsClient      insights,
        CommitmentEventBus      eventBus,
        IGraphClientFactory     graphFactory,
        ISyncStateRepository    syncState,
        IFeedbackRepository     feedbackRepo,
        ISignalProfileService   signalProfile,
        ILogger<ExtractionOrchestrator> logger)
    {
        _transcripts   = transcripts;
        _chats         = chats;
        _emails        = emails;
        _ado           = ado;
        _drive         = drive;
        _planner       = planner;
        _nlp           = nlp;
        _dedup         = dedup;
        _scorer        = scorer;
        _repo          = repo;
        _insights      = insights;
        _eventBus      = eventBus;
        _graphFactory  = graphFactory;
        _syncState     = syncState;
        _feedbackRepo  = feedbackRepo;
        _signalProfile = signalProfile;
        _logger        = logger;
    }

    public async Task<int> ExtractAndStoreAsync(string userId, string bearerToken, CancellationToken ct = default)
    {
        _logger.LogInformation("ExtractionOrchestrator: starting extraction for user {Hash}",
            PiiScrubber.HashValue(userId));

        // Load per-user signal profile (cached 5 min) — adjusts NLP thresholds and suppresses
        // titles the user has previously marked as false positives.
        var profile = await _signalProfile.GetProfileAsync(userId, ct);

        // Exchange the incoming SSO token for a Graph-scoped OBO token.
        // The raw SSO token has aud = app client ID; Graph APIs require aud = graph.microsoft.com.
        string graphToken;
        try
        {
            graphToken = await _graphFactory.GetOboTokenAsync(bearerToken, ct);
            _logger.LogDebug("ExtractionOrchestrator: OBO token acquired for user {Hash}",
                PiiScrubber.HashValue(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OBO token acquisition failed for user {Hash} — Graph extractors will be skipped",
                PiiScrubber.HashValue(userId));
            graphToken = bearerToken; // fall back; Graph calls will 401 but ADO can still run
        }

        // ── Run all 6 extractors concurrently ────────────────────────────────
        var t1 = _transcripts.GetChunksAsync(graphToken);
        var t2 = _chats.ExtractAsync(graphToken);
        var t3 = _emails.ExtractAsync(graphToken);
        var t4 = _ado.ExtractAsync(userId, bearerToken);   // ADO uses its own auth — keep SSO token
        var t5 = _drive.ExtractAsync(userId, graphToken);
        var t6 = _planner.ExtractAsync(graphToken);

        await Task.WhenAll(t1, t2, t3, t4, t5, t6);

        var transcriptChunks = t1.Result;
        var chatRaw          = t2.Result;
        var emailRaw         = t3.Result;
        var adoRaw           = t4.Result;
        var driveRaw         = t5.Result;
        var plannerRaw       = t6.Result;

        // ── NLP pipeline on transcript chunks (profile-aware) ─────────────────
        var transcriptRaw = await _nlp.ExtractFromChunksAsync(transcriptChunks, profile, ct);

        // ── Merge all sources ─────────────────────────────────────────────────
        var allRaw = transcriptRaw
            .Concat(chatRaw)
            .Concat(emailRaw)
            .Concat(adoRaw)
            .Concat(driveRaw)
            .Concat(plannerRaw)
            .ToList();

        // ── Deduplicate ────────────────────────────────────────────────────────
        var deduped = _dedup.Deduplicate(allRaw);

        // ── Suppress items the user has previously marked as false positives ──
        var suppressedCount = 0;
        if (profile.SuppressedFingerprints.Count > 0)
        {
            var before = deduped.Count;
            deduped = deduped
                .Where(r => !profile.SuppressedFingerprints.Contains(ComputeTitleFingerprint(r.Title)))
                .ToList();
            suppressedCount = before - deduped.Count;
        }

        // ── Persist with Eisenhower priority ─────────────────────────────────
        var upserted = 0;
        foreach (var raw in deduped)
        {
            var priority = _scorer.Score(raw, profile);
            // Stable RowKey so repeated extractions of the same source message
            // hit UpsertAsync (Replace) rather than creating duplicate rows.
            var stableKey = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{raw.SourceType}|{raw.SourceUrl}|{raw.SourceMetadata}")));

            var isCompletion = raw.ItemKind == ItemKind.Completion;
            var entity = new CommitmentEntity
            {
                PartitionKey    = userId,
                RowKey          = stableKey[..32],
                Title           = raw.Title,
                Owner           = userId,
                WatchersJson    = System.Text.Json.JsonSerializer.Serialize(raw.WatcherUserIds),
                SourceType      = raw.SourceType.ToString(),
                SourceUrl       = raw.SourceUrl,
                SourceMetadata  = raw.SourceMetadata,
                ProjectContext  = raw.ProjectContext,
                ArtifactName    = raw.ArtifactName,
                SourceTimestamp = raw.ExtractedAt,
                CommittedAt     = raw.ExtractedAt,
                DueAt           = raw.DueAt,
                Priority        = isCompletion ? "not-urgent-not-important" : priority,
                // Completions are already done — store immediately with status "done".
                Status          = isCompletion ? "done" : "pending",
                ItemKind        = isCompletion ? "completion" : "commitment",
                ResolutionReason = isCompletion ? $"Completed via {raw.SourceType}" : null,
                ImpactScore     = 0,
                LastActivity    = DateTimeOffset.UtcNow,
            };

            await _repo.UpsertAsync(entity, ct);
            upserted++;
        }

        // Notify any connected SSE client that new items are ready
        _eventBus.Publish(userId, upserted);

        _insights.TrackUserAction("extract-background", PiiScrubber.HashValue(userId), "commitments",
            new Dictionary<string, string>
            {
                ["raw"]        = allRaw.Count.ToString(),
                ["deduped"]    = deduped.Count.ToString(),
                ["stored"]     = upserted.ToString(),
                ["suppressed"] = suppressedCount.ToString(),
                ["sources"]    = "transcript,chat,email,ado,drive,planner",
                ["confAdj"]    = profile.ConfidenceAdjustment.ToString("F2"),
            });

        _logger.LogInformation(
            "ExtractionOrchestrator: {Raw} raw → {Deduped} deduped → {Suppressed} suppressed → {Stored} stored for user {Hash}",
            allRaw.Count, deduped.Count, suppressedCount, upserted, PiiScrubber.HashValue(userId));

        return upserted;
    }

    /// <summary>
    /// Computes a short stable fingerprint of a commitment title for suppression checks.
    /// Normalises to lowercase token-sorted tokens, then SHA-256, truncated to 16 hex chars.
    /// </summary>
    internal static string ComputeTitleFingerprint(string title)
    {
        var normalized = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ')
            .ToArray());
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(" ", tokens.OrderBy(t => t));
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(joined)))[..16];
    }
}
