namespace CommitApi.Models.Extraction;

/// <summary>Commitment discovered from a signal source, before deduplication.</summary>
public record RawCommitment(
    /// <summary>Normalized task title (≤200 chars, no PII logged).</summary>
    string Title,

    /// <summary>AAD OID of the person who committed.</summary>
    string OwnerUserId,

    /// <summary>Display name for UI rendering only — never logged.</summary>
    string OwnerDisplayName,

    /// <summary>Origin of this commitment.</summary>
    CommitmentSourceType SourceType,

    /// <summary>Deep-link URL to the original message / email / PR.</summary>
    string SourceUrl,

    /// <summary>When the commitment was extracted.</summary>
    DateTimeOffset ExtractedAt,

    /// <summary>Inferred due date, if any.</summary>
    DateTimeOffset? DueAt,

    /// <summary>NLP confidence 0–1. Below 0.6 → discard.</summary>
    double Confidence,

    /// <summary>AAD OIDs of additional people mentioned (watchers).</summary>
    string[] WatcherUserIds,

    /// <summary>Short excerpt for display. Max 200 chars (P-09: no full body).</summary>
    string SourceContext
);

/// <summary>Which signal source produced a <see cref="RawCommitment"/>.</summary>
public enum CommitmentSourceType
{
    Transcript,
    Chat,
    Email,
    Ado
}
