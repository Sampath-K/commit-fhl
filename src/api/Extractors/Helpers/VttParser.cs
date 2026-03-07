using CommitApi.Models.Extraction;

namespace CommitApi.Extractors.Helpers;

/// <summary>
/// Parses WebVTT transcript content into per-speaker <see cref="TranscriptChunk"/> objects.
/// Extracted from <see cref="TranscriptExtractor"/> for testability.
/// </summary>
internal static class VttParser
{
    /// <summary>
    /// Parses a VTT string into a list of transcript chunks grouped by speaker.
    /// </summary>
    public static List<TranscriptChunk> Parse(string vtt, string meetingId, string? meetingSubject)
    {
        var chunks = new List<TranscriptChunk>();
        var lines  = vtt.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var cueTimestamp = DateTimeOffset.UtcNow;
        string? lastSpeaker = null;
        string? lastUserId  = null;
        var textBuffer = new System.Text.StringBuilder();

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
            if (line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)) continue;

            if (line.Contains("-->"))
            {
                FlushChunk();
                var parts = line.Split("-->")[0].Trim();
                if (TimeSpan.TryParse(parts, out var ts))
                    cueTimestamp = DateTimeOffset.UtcNow.Add(ts);
                continue;
            }

            if (line.Contains(':'))
            {
                var colonIdx = line.IndexOf(':');
                var speaker  = line[..colonIdx].Trim();
                var text     = line[(colonIdx + 1)..].Trim();

                if (speaker != lastSpeaker)
                {
                    FlushChunk();
                    lastSpeaker = speaker;
                    lastUserId  = ExtractUserId(speaker);
                    lastSpeaker = CleanSpeakerName(speaker);
                }

                textBuffer.Append(text).Append(' ');
            }
        }

        FlushChunk();
        return chunks;
    }

    /// <summary>Extracts a userId embedded as "&lt;userId&gt;" in the speaker field.</summary>
    internal static string? ExtractUserId(string speaker)
    {
        var start = speaker.IndexOf('<');
        var end   = speaker.IndexOf('>');
        return start >= 0 && end > start ? speaker[(start + 1)..end] : null;
    }

    /// <summary>Strips the "&lt;userId&gt;" suffix from a speaker name.</summary>
    internal static string CleanSpeakerName(string speaker)
    {
        var start = speaker.IndexOf('<');
        return start > 0 ? speaker[..start].Trim() : speaker.Trim();
    }
}
