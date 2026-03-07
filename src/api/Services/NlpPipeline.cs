using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using CommitApi.Exceptions;
using CommitApi.Models.Extraction;
using CommitApi.Models.Feedback;
using OpenAI.Chat;

namespace CommitApi.Services;

/// <summary>
/// Uses Azure OpenAI GPT-4o to extract structured commitments from text.
/// </summary>
public sealed class NlpPipeline : INlpPipeline
{
    private readonly AzureOpenAIClient? _aiClient;
    private readonly ILogger<NlpPipeline> _logger;
    private readonly string _deployment;

    // Minimum confidence to keep a commitment
    private const double MinConfidence = 0.6;

    public NlpPipeline(
        ILogger<NlpPipeline> logger,
        IConfiguration config)
    {
        _logger     = logger;
        _deployment = config["AZURE_OPENAI_DEPLOYMENT"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";

        var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var key      = config["AZURE_OPENAI_KEY"]      ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");

        if (endpoint is not null && key is not null)
        {
            _aiClient = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(key));
        }
        else
        {
            _logger.LogWarning("AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_KEY not set — NLP pipeline disabled");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractFromChunksAsync(
        IEnumerable<TranscriptChunk> chunks,
        UserSignalProfile? profile = null,
        CancellationToken ct = default)
    {
        if (_aiClient is null) return [];

        var effectiveMin = MinConfidence + (profile?.ConfidenceAdjustment ?? 0.0);
        var systemPrompt = BuildSystemPrompt(profile);
        var sw           = Stopwatch.StartNew();
        var groups       = chunks.GroupBy(c => c.MeetingId);
        var results      = new List<RawCommitment>();

        foreach (var meeting in groups)
        {
            var transcript = BuildTranscriptText(meeting);
            if (transcript.Length < 50) continue;

            var extracted = await CallOpenAiAsync(transcript, CommitmentSourceType.Transcript, effectiveMin, systemPrompt, ct);
            results.AddRange(extracted.Where(c => c.Confidence >= effectiveMin));
        }

        _logger.LogInformation(
            "NLP pipeline: {Count} chunks in {Ms}ms (target <300000ms)",
            results.Count, sw.ElapsedMilliseconds);

        return results;
    }

    /// <inheritdoc/>
    public async Task<RawCommitment?> RefineAsync(
        RawCommitment heuristic,
        UserSignalProfile? profile = null,
        CancellationToken ct = default)
    {
        if (_aiClient is null) return heuristic;

        var effectiveMin = MinConfidence + (profile?.ConfidenceAdjustment ?? 0.0);
        var systemPrompt = BuildSystemPrompt(profile);

        var prompt = $$"""
            Text: "{{heuristic.SourceContext}}"
            This text was flagged as containing a commitment. Assess the confidence (0-1)
            that this represents a real task commitment and provide a clean title.
            Respond with JSON only: { "title": "...", "confidence": 0.x, "dueDate": "ISO8601 or null" }
            """;

        try
        {
            var client   = _aiClient.GetChatClient(_deployment);
            var response = await client.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt)
            ], cancellationToken: ct);

            var text = response.Value.Content[0].Text;
            using var doc = JsonDocument.Parse(ExtractJson(text));

            var confidence = doc.RootElement.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : heuristic.Confidence;
            if (confidence < effectiveMin) return null;

            var title   = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() ?? heuristic.Title : heuristic.Title;
            var dueDate = doc.RootElement.TryGetProperty("dueDate", out var d) && d.GetString() is { } ds && ds != "null"
                ? DateTimeOffset.TryParse(ds, out var dt) ? dt : heuristic.DueAt
                : heuristic.DueAt;

            return heuristic with { Title = title, Confidence = confidence, DueAt = dueDate };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NLP refine failed — keeping heuristic result");
            return heuristic;
        }
    }

    private async Task<List<RawCommitment>> CallOpenAiAsync(
        string text,
        CommitmentSourceType sourceType,
        double effectiveMin,
        string systemPrompt,
        CancellationToken ct)
    {
        var prompt = $"""
            Extract all commitments from the following text. A commitment is a promise,
            action item, or task that a specific person has agreed to complete.

            For each commitment return a JSON object with:
            - title: concise normalized task (≤80 chars, no PII)
            - ownerName: display name of person who committed
            - dueDate: ISO 8601 date if mentioned, otherwise null
            - confidence: 0-1 likelihood this is a real commitment
            - watchers: array of other people mentioned who care about this task

            Return a JSON array. Only include items with confidence >= {effectiveMin:F2}.

            Text:
            {text[..Math.Min(text.Length, 3000)]}
            """;

        try
        {
            var client   = _aiClient!.GetChatClient(_deployment);
            var response = await client.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt)
            ], cancellationToken: ct);

            var responseText = response.Value.Content[0].Text;
            var jsonArray    = ExtractJsonArray(responseText);
            using var doc    = JsonDocument.Parse(jsonArray);

            var results = new List<RawCommitment>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var title      = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var ownerName  = item.TryGetProperty("ownerName", out var on) ? on.GetString() ?? "Unknown" : "Unknown";
                var confidence = item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;
                var dueDate    = item.TryGetProperty("dueDate", out var d) && d.GetString() is { } ds && ds != "null"
                    ? DateTimeOffset.TryParse(ds, out var dt) ? (DateTimeOffset?)dt : null
                    : null;
                var watchers   = item.TryGetProperty("watchers", out var w)
                    ? w.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToArray()
                    : [];

                if (string.IsNullOrWhiteSpace(title) || confidence < effectiveMin) continue;

                results.Add(new RawCommitment(
                    Title:            title,
                    OwnerUserId:      ownerName,  // resolved to OID in next step
                    OwnerDisplayName: ownerName,
                    SourceType:       sourceType,
                    SourceUrl:        "",
                    ExtractedAt:      DateTimeOffset.UtcNow,
                    DueAt:            dueDate,
                    Confidence:       confidence,
                    WatcherUserIds:   watchers,
                    SourceContext:    text[..Math.Min(text.Length, 200)]));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NLP extraction call failed");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ResolutionClassification>> ClassifyResolutionAsync(
        IReadOnlyList<string> commitmentTitles,
        IReadOnlyList<string[]> followUpMessages,
        CancellationToken ct = default)
    {
        // Default: unresolved, low confidence — returned when OpenAI is unavailable
        var defaults = commitmentTitles
            .Select(_ => new ResolutionClassification(false, 0.0, "OpenAI unavailable"))
            .ToList();

        if (_aiClient is null || commitmentTitles.Count == 0) return defaults;

        // Build a compact batch prompt — all commitments in one call
        var sb = new StringBuilder();
        sb.AppendLine("Commitments to check:");
        for (var i = 0; i < commitmentTitles.Count; i++)
            sb.AppendLine($"[{i}] \"{commitmentTitles[i]}\"");

        sb.AppendLine("\nFollow-up messages from the same person (per commitment):");
        for (var i = 0; i < followUpMessages.Count; i++)
        {
            var msgs = followUpMessages[i];
            if (msgs.Length == 0) { sb.AppendLine($"[{i}] (none)"); continue; }
            sb.AppendLine($"[{i}]");
            foreach (var m in msgs.Take(5))
                sb.AppendLine($"  - \"{m[..Math.Min(m.Length, 150)]}\"");
        }

        sb.AppendLine("""

            For each commitment index, respond ONLY with a JSON array:
            [{"index":0,"resolved":true,"confidence":0.95,"evidence":"sent the report in message #1"},...]
            resolved=true only when there is clear evidence the task was completed.
            confidence is 0.0–1.0. evidence is ≤60 chars.
            """);

        try
        {
            var client   = _aiClient.GetChatClient(_deployment);
            var response = await client.CompleteChatAsync(
            [
                new SystemChatMessage(ResolutionSystemPrompt),
                new UserChatMessage(sb.ToString()),
            ], cancellationToken: ct);

            var text = response.Value.Content[0].Text;
            var json = ExtractJsonArray(text);
            using var doc = JsonDocument.Parse(json);

            var results = new ResolutionClassification[commitmentTitles.Count];
            for (var i = 0; i < results.Length; i++)
                results[i] = defaults[i];

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var idx = item.TryGetProperty("index", out var idx2) ? idx2.GetInt32() : -1;
                if (idx < 0 || idx >= results.Length) continue;

                var resolved   = item.TryGetProperty("resolved",   out var r) && r.GetBoolean();
                var confidence = item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;
                var evidence   = item.TryGetProperty("evidence",   out var e) ? e.GetString() ?? "" : "";
                results[idx]   = new ResolutionClassification(resolved, confidence, evidence);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NLP resolution classification failed — returning defaults");
            return defaults;
        }
    }

    private static string BuildSystemPrompt(UserSignalProfile? profile)
    {
        if (profile is null || (profile.NlpPositiveExamples.Count == 0 && profile.NlpNegativeExamples.Count == 0))
            return SystemPrompt;

        var sb = new StringBuilder(SystemPrompt);

        if (profile.NlpPositiveExamples.Count > 0)
        {
            sb.AppendLine("\nPOSITIVE EXAMPLES (extract commitments like these):");
            foreach (var ex in profile.NlpPositiveExamples)
                sb.AppendLine($"- \"{ex}\"");
        }

        if (profile.NlpNegativeExamples.Count > 0)
        {
            sb.AppendLine("\nNEGATIVE EXAMPLES (do not extract these — confirmed non-commitments for this user):");
            foreach (var ex in profile.NlpNegativeExamples)
                sb.AppendLine($"- \"{ex}\"");
        }

        return sb.ToString();
    }

    private static string BuildTranscriptText(IEnumerable<TranscriptChunk> chunks)
    {
        var sb = new StringBuilder();
        foreach (var chunk in chunks)
            sb.AppendLine($"{chunk.SpeakerName}: {chunk.Text}");
        return sb.ToString();
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : "{}";
    }

    private static string ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[');
        var end   = text.LastIndexOf(']');
        return start >= 0 && end > start ? text[start..(end + 1)] : "[]";
    }

    private const string ResolutionSystemPrompt = """
        You are a completion classifier for a professional commitment tracking tool.
        Your job is to determine whether a person has completed a specific task based on
        their recent follow-up messages. Be conservative: only mark resolved=true when
        there is clear, unambiguous evidence that the task was finished.
        Always respond with valid JSON array only.
        """;

    private const string SystemPrompt = """
        You are a commitment extractor for a professional productivity tool.
        Your job is to identify clear, explicit task commitments — not vague intentions or discussions.
        Focus on statements where someone has clearly agreed to do a specific task.
        Always respond with valid JSON only.
        """;
}
