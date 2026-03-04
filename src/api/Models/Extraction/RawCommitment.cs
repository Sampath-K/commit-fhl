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
    string SourceContext,

    /// <summary>
    /// JSON bag of source-system identifiers used for same-source resolution (Tier 1.5).
    /// Chat:  { "chatId", "messageId", "chatType" }
    /// Email: { "messageId", "conversationId" }
    /// Drive: { "itemId", "commentId" }
    /// Null for sources with no re-queryable identifier (Transcript, ADO, Planner).
    /// </summary>
    string? SourceMetadata = null,

    /// <summary>
    /// Inferred project or workspace bucket this commitment belongs to.
    /// Drive: deepest folder name from parentReference.path (e.g. "Q2 Planning")
    /// Chat:  Teams team display name (e.g. "Platform Engineering")
    /// Email: null (no reliable inference without user-defined rules)
    /// Planner: plan title (e.g. "Sprint 42")
    /// Transcript: meeting subject prefix
    /// </summary>
    string? ProjectContext = null,

    /// <summary>
    /// The specific artifact (file, thread, channel, meeting) that generated this commitment.
    /// Drive:      file name ("roadmap.docx")
    /// Chat DM/group: chat topic or participants
    /// Chat channel:  "#channel-name"
    /// Email:      email subject
    /// Planner:    task title (same as Title)
    /// Transcript: meeting subject
    /// </summary>
    string? ArtifactName = null,

    /// <summary>
    /// Whether this is an external commitment (default) or a completed deliverable.
    /// Completions are stored immediately with Status = "done" and appear in the Progress view.
    /// </summary>
    ItemKind ItemKind = ItemKind.Commitment
);

/// <summary>Which signal source produced a <see cref="RawCommitment"/>.</summary>
public enum CommitmentSourceType
{
    Transcript,
    Chat,
    Email,
    Ado,
    Drive,    // OneDrive/SharePoint Office documents (WXP)
    Planner,  // Microsoft Planner / Loop tasks
}

/// <summary>
/// Whether a raw signal is an external commitment or a completed deliverable.
/// </summary>
public enum ItemKind
{
    /// <summary>The user committed to do something (default — extracted from meetings, chat, email, ADO review threads).</summary>
    Commitment,
    /// <summary>The user completed a deliverable (merged PR, closed ADO task, completed Planner task).</summary>
    Completion,
}
