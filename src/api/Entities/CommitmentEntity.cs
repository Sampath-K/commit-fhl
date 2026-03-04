using Azure;
using Azure.Data.Tables;

namespace CommitApi.Entities;

/// <summary>
/// Azure Table Storage entity for a tracked commitment.
/// PartitionKey = owner AAD Object ID.
/// RowKey = UUID commitment ID.
/// Arrays (watchers, blockedBy, blocks) serialized as JSON strings — Azure Tables has no array type.
/// </summary>
public class CommitmentEntity : ITableEntity
{
    // ─── ITableEntity ──────────────────────────────────────────────────────────
    /// <inheritdoc /> PartitionKey = owner AAD Object ID.
    public string PartitionKey { get; set; } = string.Empty;
    /// <inheritdoc /> RowKey = commitment UUID.
    public string RowKey { get; set; } = string.Empty;
    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }
    /// <inheritdoc />
    public ETag ETag { get; set; }

    // ─── Core fields ───────────────────────────────────────────────────────────
    /// <summary>Normalized task title. Never include in telemetry (P-12).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>AAD Object ID of commitment owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>JSON-serialized string[] of watcher AAD Object IDs.</summary>
    public string WatchersJson { get; set; } = "[]";

    // ─── Source provenance ─────────────────────────────────────────────────────
    public string SourceType { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTimeOffset SourceTimestamp { get; set; }
    public string? SourceId { get; set; }

    /// <summary>
    /// JSON bag of source-system API identifiers for Tier 1.5 same-source resolution.
    /// Chat:  {"chatId":"…","messageId":"…","chatType":"dm|channel","teamId":"…","channelId":"…"}
    /// Email: {"messageId":"…","conversationId":"…"}
    /// Drive: {"itemId":"…","commentId":"…"}
    /// </summary>
    public string? SourceMetadata { get; set; }

    /// <summary>Inferred project/workspace bucket. Used for "By Project" UI grouping.</summary>
    public string? ProjectContext { get; set; }

    /// <summary>Specific artifact that generated this commitment (file, thread, meeting, etc.).</summary>
    public string? ArtifactName { get; set; }

    // ─── Lifecycle ─────────────────────────────────────────────────────────────
    public DateTimeOffset CommittedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>pending | in-progress | done | deferred | delegated</summary>
    public string Status { get; set; } = "pending";

    /// <summary>urgent-important | not-urgent-important | urgent-not-important | not-urgent-not-important</summary>
    public string Priority { get; set; } = "not-urgent-not-important";

    // ─── Dependency graph ──────────────────────────────────────────────────────
    /// <summary>JSON-serialized string[] of commitment IDs this is blocked by.</summary>
    public string BlockedByJson { get; set; } = "[]";

    /// <summary>JSON-serialized string[] of commitment IDs this blocks.</summary>
    public string BlocksJson { get; set; } = "[]";

    // ─── Item kind ─────────────────────────────────────────────────────────────
    /// <summary>"commitment" | "completion" — external obligation vs. completed deliverable.</summary>
    public string ItemKind { get; set; } = "commitment";

    // ─── Resolution provenance ─────────────────────────────────────────────────
    /// <summary>
    /// Human-readable explanation of how this commitment was auto-resolved.
    /// Shown in the UI as the "aha moment" — e.g. "PR #123 was completed in ADO"
    /// or "You said 'just sent it!' in your chat with Alex 12 min ago".
    /// Null when manually resolved or not yet resolved.
    /// </summary>
    public string? ResolutionReason { get; set; }

    // ─── Scoring ───────────────────────────────────────────────────────────────
    public int ImpactScore { get; set; }
    public double BurnoutContribution { get; set; }
    public DateTimeOffset? LastActivity { get; set; }

    // ─── Agent draft ───────────────────────────────────────────────────────────
    /// <summary>JSON-serialized AgentDraftEntity, if a draft is pending.</summary>
    public string? AgentDraftJson { get; set; }

    public int? OwnerDeliveryScoreAtCreation { get; set; }
}
