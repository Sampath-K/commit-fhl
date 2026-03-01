namespace CommitApi.Models.Extraction;

/// <summary>
/// A single speaker turn extracted from a meeting transcript.
/// </summary>
public record TranscriptChunk(
    string SpeakerName,
    string SpeakerUserId,
    string Text,
    string MeetingId,
    DateTimeOffset Timestamp,
    string? MeetingSubject
);
