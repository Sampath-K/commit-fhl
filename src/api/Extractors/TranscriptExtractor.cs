using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Auth;
using CommitApi.Exceptions;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Fetches Teams meeting transcripts via Microsoft Graph (beta) and splits
/// them into per-speaker <see cref="TranscriptChunk"/> objects.
/// </summary>
public sealed class TranscriptExtractor : ITranscriptExtractor
{
    private readonly IGraphClientFactory _graphFactory;
    private readonly ILogger<TranscriptExtractor> _logger;
    private readonly HttpClient _http;

    // Graph beta endpoint — online-meeting transcripts not yet in v1.0
    private const string GraphBeta = "https://graph.microsoft.com/beta";

    public TranscriptExtractor(
        IGraphClientFactory graphFactory,
        ILogger<TranscriptExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _graphFactory   = graphFactory;
        _logger         = logger;
        _http           = httpClientFactory.CreateClient("graph");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TranscriptChunk>> GetChunksAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days).ToString("o");

        // Step 1 — list online meetings from last N days
        var meetingsUrl = $"{GraphBeta}/me/onlineMeetings" +
                          $"?$filter=startDateTime ge {Uri.EscapeDataString(since)}" +
                          $"&$select=id,subject,startDateTime&$top=20";

        using var meetingsReq = new HttpRequestMessage(HttpMethod.Get, meetingsUrl);
        meetingsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var meetingsResp = await _http.SendAsync(meetingsReq, ct);

        if (!meetingsResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Graph meetings returned {Status}", meetingsResp.StatusCode);
            return [];
        }

        var meetingsJson = await meetingsResp.Content.ReadAsStringAsync(ct);
        using var meetingsDoc = JsonDocument.Parse(meetingsJson);

        var chunks = new List<TranscriptChunk>();

        foreach (var meeting in meetingsDoc.RootElement.GetProperty("value").EnumerateArray())
        {
            var meetingId      = meeting.GetProperty("id").GetString() ?? "";
            var meetingSubject = meeting.TryGetProperty("subject", out var subj)
                ? subj.GetString()
                : null;

            // Step 2 — fetch transcript content (VTT format)
            var transcriptUrl = $"{GraphBeta}/me/onlineMeetings/{meetingId}/transcripts/content?$format=text/vtt";
            using var transcriptReq = new HttpRequestMessage(HttpMethod.Get, transcriptUrl);
            transcriptReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var transcriptResp = await _http.SendAsync(transcriptReq, ct);

            if (!transcriptResp.IsSuccessStatusCode)
            {
                // Transcript may not be available for all meetings — skip silently
                continue;
            }

            var vttContent = await transcriptResp.Content.ReadAsStringAsync(ct);
            var meetingChunks = ParseVtt(vttContent, meetingId, meetingSubject);
            chunks.AddRange(meetingChunks);
        }

        _logger.LogInformation("Transcript extractor: {Count} chunks from last {Days}d", chunks.Count, days);
        return chunks;
    }

    /// <summary>
    /// Parses WebVTT transcript content into per-speaker chunks.
    /// VTT format: speaker lines begin with "WEBVTT" followed by cue blocks.
    /// </summary>
    private static List<TranscriptChunk> ParseVtt(
        string vtt,
        string meetingId,
        string? meetingSubject)
    {
        var chunks = new List<TranscriptChunk>();
        var lines  = vtt.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Each cue block: timestamp line → "SpeakerName: text" line(s)
        var cueTimestamp = DateTimeOffset.UtcNow; // fallback
        string? lastSpeaker = null;
        string? lastUserId  = null;
        var textBuffer      = new System.Text.StringBuilder();

        void FlushChunk()
        {
            if (lastSpeaker is null || textBuffer.Length == 0) return;
            chunks.Add(new TranscriptChunk(
                SpeakerName:    lastSpeaker,
                SpeakerUserId:  lastUserId ?? lastSpeaker,
                Text:           textBuffer.ToString().Trim(),
                MeetingId:      meetingId,
                Timestamp:      cueTimestamp,
                MeetingSubject: meetingSubject));
            textBuffer.Clear();
        }

        foreach (var line in lines)
        {
            // Skip WEBVTT header and NOTE lines
            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)) continue;

            // Timestamp line: "00:00:00.000 --> 00:00:05.000"
            if (line.Contains("-->"))
            {
                FlushChunk();
                // Parse start time if possible
                var parts = line.Split("-->")[0].Trim();
                if (TimeSpan.TryParse(parts, out var ts))
                    cueTimestamp = DateTimeOffset.UtcNow.Add(ts); // relative for now
                continue;
            }

            // Speaker line: "<SpeakerName> <userId>: text" or "SpeakerName: text"
            if (line.Contains(':'))
            {
                var colonIdx = line.IndexOf(':');
                var speaker  = line[..colonIdx].Trim();
                var text     = line[(colonIdx + 1)..].Trim();

                if (speaker != lastSpeaker)
                {
                    FlushChunk();
                    lastSpeaker = speaker;
                    // userId embedded as "<userId>" in some VTT flavours
                    lastUserId = ExtractUserId(speaker);
                    lastSpeaker = CleanSpeakerName(speaker);
                }

                textBuffer.Append(text).Append(' ');
            }
        }

        FlushChunk();
        return chunks;
    }

    private static string? ExtractUserId(string speaker)
    {
        var start = speaker.IndexOf('<');
        var end   = speaker.IndexOf('>');
        return start >= 0 && end > start ? speaker[(start + 1)..end] : null;
    }

    private static string CleanSpeakerName(string speaker)
    {
        var start = speaker.IndexOf('<');
        return start > 0 ? speaker[..start].Trim() : speaker.Trim();
    }
}
