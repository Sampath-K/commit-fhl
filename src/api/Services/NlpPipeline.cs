using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using CommitApi.Exceptions;
using CommitApi.Models.Extraction;
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
        CancellationToken ct = default)
    {
        if (_aiClient is null) return [];

        var groups = chunks.GroupBy(c => c.MeetingId);
        var results = new List<RawCommitment>();

        foreach (var meeting in groups)
        {
            var transcript = BuildTranscriptText(meeting);
            if (transcript.Length < 50) continue;

            var extracted = await CallOpenAiAsync(transcript, CommitmentSourceType.Transcript, ct);
            results.AddRange(extracted.Where(c => c.Confidence >= MinConfidence));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<RawCommitment?> RefineAsync(
        RawCommitment heuristic,
        CancellationToken ct = default)
    {
        if (_aiClient is null) return heuristic;

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
                new SystemChatMessage(SystemPrompt),
                new UserChatMessage(prompt)
            ], cancellationToken: ct);

            var text = response.Value.Content[0].Text;
            using var doc = JsonDocument.Parse(ExtractJson(text));

            var confidence = doc.RootElement.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : heuristic.Confidence;
            if (confidence < MinConfidence) return null;

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

            Return a JSON array. Only include items with confidence >= 0.6.

            Text:
            {text[..Math.Min(text.Length, 3000)]}
            """;

        try
        {
            var client   = _aiClient!.GetChatClient(_deployment);
            var response = await client.CompleteChatAsync(
            [
                new SystemChatMessage(SystemPrompt),
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

                if (string.IsNullOrWhiteSpace(title) || confidence < MinConfidence) continue;

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

    private const string SystemPrompt = """
        You are a commitment extractor for a professional productivity tool.
        Your job is to identify clear, explicit task commitments — not vague intentions or discussions.
        Focus on statements where someone has clearly agreed to do a specific task.
        Always respond with valid JSON only.
        """;
}
