using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Extractors;
using CommitApi.Repositories;
using CommitApi.Services;

namespace CommitApi.Services;

/// <summary>
/// Orchestrates a full extraction run for a single user:
///   1. Runs all 4 extractors concurrently (transcript, chat, email, ADO)
///   2. Runs NLP pipeline on transcript chunks
///   3. Deduplicates across all sources
///   4. Scores priority (Eisenhower)
///   5. Persists to Azure Table Storage
///
/// This is the shared extraction logic used by:
///   - POST /api/v1/extract  (user-initiated, with fresh token)
///   - ExtractionPollingService (5-min background polling)
///   - WebhookHandler (webhook-triggered on Teams/email notifications)
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
    private readonly INlpPipeline            _nlp;
    private readonly IDeduplicationService   _dedup;
    private readonly IEisenhowerScorer       _scorer;
    private readonly ICommitmentRepository   _repo;
    private readonly IAppInsightsClient      _insights;
    private readonly ILogger<ExtractionOrchestrator> _logger;

    public ExtractionOrchestrator(
        ITranscriptExtractor    transcripts,
        IChatExtractor          chats,
        IEmailExtractor         emails,
        IAdoExtractor           ado,
        INlpPipeline            nlp,
        IDeduplicationService   dedup,
        IEisenhowerScorer       scorer,
        ICommitmentRepository   repo,
        IAppInsightsClient      insights,
        ILogger<ExtractionOrchestrator> logger)
    {
        _transcripts = transcripts;
        _chats       = chats;
        _emails      = emails;
        _ado         = ado;
        _nlp         = nlp;
        _dedup       = dedup;
        _scorer      = scorer;
        _repo        = repo;
        _insights    = insights;
        _logger      = logger;
    }

    public async Task<int> ExtractAndStoreAsync(string userId, string bearerToken, CancellationToken ct = default)
    {
        _logger.LogInformation("ExtractionOrchestrator: starting extraction for user {Hash}",
            PiiScrubber.HashValue(userId));

        // ── Run all 4 extractors concurrently ────────────────────────────────
        var t1 = _transcripts.GetChunksAsync(bearerToken);
        var t2 = _chats.ExtractAsync(bearerToken);
        var t3 = _emails.ExtractAsync(bearerToken);
        var t4 = _ado.ExtractAsync(userId, bearerToken);

        await Task.WhenAll(t1, t2, t3, t4);

        var transcriptChunks = t1.Result;
        var chatRaw          = t2.Result;
        var emailRaw         = t3.Result;
        var adoRaw           = t4.Result;

        // ── NLP pipeline on transcript chunks ────────────────────────────────
        var transcriptRaw = await _nlp.ExtractFromChunksAsync(transcriptChunks, ct);

        // ── Merge all sources ─────────────────────────────────────────────────
        var allRaw = transcriptRaw
            .Concat(chatRaw)
            .Concat(emailRaw)
            .Concat(adoRaw)
            .ToList();

        // ── Deduplicate ────────────────────────────────────────────────────────
        var deduped = _dedup.Deduplicate(allRaw);

        // ── Persist with Eisenhower priority ─────────────────────────────────
        var upserted = 0;
        foreach (var raw in deduped)
        {
            var priority = _scorer.Score(raw);
            var entity = new CommitmentEntity
            {
                PartitionKey    = userId,
                RowKey          = Guid.NewGuid().ToString(),
                Title           = raw.Title,
                Owner           = userId,
                WatchersJson    = System.Text.Json.JsonSerializer.Serialize(raw.WatcherUserIds),
                SourceType      = raw.SourceType.ToString(),
                SourceUrl       = raw.SourceUrl,
                SourceTimestamp = raw.ExtractedAt,
                CommittedAt     = raw.ExtractedAt,
                DueAt           = raw.DueAt,
                Priority        = priority,
                Status          = "pending",
                ImpactScore     = 0,
                LastActivity    = DateTimeOffset.UtcNow,
            };

            await _repo.UpsertAsync(entity, ct);
            upserted++;
        }

        _insights.TrackUserAction("extract-background", PiiScrubber.HashValue(userId), "commitments",
            new Dictionary<string, string>
            {
                ["raw"]     = allRaw.Count.ToString(),
                ["deduped"] = deduped.Count.ToString(),
                ["stored"]  = upserted.ToString(),
                ["sources"] = "transcript,chat,email,ado",
            });

        _logger.LogInformation(
            "ExtractionOrchestrator: extracted {Raw} raw → {Deduped} deduped → {Stored} stored for user {Hash}",
            allRaw.Count, deduped.Count, upserted, PiiScrubber.HashValue(userId));

        return upserted;
    }
}
