using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Auth;
using CommitApi.Exceptions;
using CommitApi.Extractors.Helpers;
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

    private static List<TranscriptChunk> ParseVtt(
        string vtt,
        string meetingId,
        string? meetingSubject)
        => VttParser.Parse(vtt, meetingId, meetingSubject);
}
