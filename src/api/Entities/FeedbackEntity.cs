using Azure;
using Azure.Data.Tables;

namespace CommitApi.Entities;

/// <summary>
/// Stores a single user feedback event in Azure Table Storage (table: feedback).
/// PII compliance: no raw text stored; all identifiers are SHA-256 hashed.
/// PK = SHA-256(userId), RK = Guid (unique per feedback event).
/// </summary>
public sealed class FeedbackEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey       { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>SHA-256(commitmentId)[..16] — never raw ID.</summary>
    public string CommitmentIdHash { get; set; } = "";

    /// <summary>SHA-256 of the normalized title token set — one-way hash, no raw text.</summary>
    public string TitleFingerprint { get; set; } = "";

    /// <summary>Feedback type: FalsePositive, Confirm, WrongOwner, Duplicate.</summary>
    public string FeedbackType { get; set; } = "";

    /// <summary>Source extractor type: Chat, Email, Ado, Transcript, Drive, Planner.</summary>
    public string SourceType { get; set; } = "";

    /// <summary>When this feedback was recorded (UTC).</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>NLP confidence score of the commitment at the time feedback was given.</summary>
    public double ConfidenceAtFeedback { get; set; }
}
