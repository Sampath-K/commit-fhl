using CommitApi.Auth;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Extractors;
using CommitApi.Models.Extraction;
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
        ILogger<ExtractionOrchestrator> logger)
    {
        _transcripts  = transcripts;
        _chats        = chats;
        _emails       = emails;
        _ado          = ado;
        _drive        = drive;
        _planner      = planner;
        _nlp          = nlp;
        _dedup        = dedup;
        _scorer       = scorer;
        _repo         = repo;
        _insights     = insights;
        _eventBus     = eventBus;
        _graphFactory = graphFactory;
        _logger       = logger;
    }

    public async Task<int> ExtractAndStoreAsync(string userId, string bearerToken, CancellationToken ct = default)
    {
        _logger.LogInformation("ExtractionOrchestrator: starting extraction for user {Hash}",
            PiiScrubber.HashValue(userId));

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

        // ── NLP pipeline on transcript chunks ────────────────────────────────
        var transcriptRaw = await _nlp.ExtractFromChunksAsync(transcriptChunks, ct);

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

        // ── Persist with Eisenhower priority ─────────────────────────────────
        var upserted = 0;
        foreach (var raw in deduped)
        {
            var priority = _scorer.Score(raw);
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
                ["raw"]     = allRaw.Count.ToString(),
                ["deduped"] = deduped.Count.ToString(),
                ["stored"]  = upserted.ToString(),
                ["sources"] = "transcript,chat,email,ado,drive,planner",
            });

        _logger.LogInformation(
            "ExtractionOrchestrator: extracted {Raw} raw → {Deduped} deduped → {Stored} stored for user {Hash}",
            allRaw.Count, deduped.Count, upserted, PiiScrubber.HashValue(userId));

        return upserted;
    }
}
