namespace CommitApi.Models;

/// <summary>
/// Request body for POST /api/v1/commitments — upsert a single commitment.
/// Used by the seed script and direct commitment creation.
/// Matches the frontend CommitmentRecord TypeScript interface.
/// </summary>
public sealed class CommitmentUpsertDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Owner { get; set; }
    public string[]? Watchers { get; set; }
    public CommitmentSourceInput? Source { get; set; }
    public string? CommittedAt { get; set; }
    public string? DueAt { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string[]? BlockedBy { get; set; }
    public string[]? Blocks { get; set; }
    public int ImpactScore { get; set; }
    public double BurnoutContribution { get; set; }
    public string? LastActivity { get; set; }
    public int? OwnerDeliveryScoreAtCreation { get; set; }
}

/// <summary>Source provenance input from seed/creation payload.</summary>
public sealed class CommitmentSourceInput
{
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Timestamp { get; set; }
    public string? SourceId { get; set; }
}
