using System.Text.Json;
using CommitApi.Entities;

namespace CommitApi.Models;

/// <summary>
/// API response DTO matching the frontend <c>CommitmentRecord</c> TypeScript interface.
/// Maps from <see cref="CommitmentEntity"/> (storage) to a clean JSON-serializable shape.
/// </summary>
public record CommitmentResponse(
    string Id,
    string Title,
    string Owner,
    string[] Watchers,
    CommitmentSourceDto Source,
    string CommittedAt,
    string? DueAt,
    string Status,
    string Priority,
    string[] BlockedBy,
    string[] Blocks,
    int ImpactScore,
    double BurnoutContribution,
    string? LastActivity,
    int? OwnerDeliveryScoreAtCreation
)
{
    /// <summary>Maps a storage entity to the frontend DTO.</summary>
    public static CommitmentResponse From(CommitmentEntity e) => new(
        Id:                           e.RowKey,
        Title:                        e.Title,
        Owner:                        e.Owner,
        Watchers:                     Deserialize(e.WatchersJson),
        Source:                       new CommitmentSourceDto(
                                          MapSourceType(e.SourceType),
                                          e.SourceUrl,
                                          e.SourceTimestamp.ToString("o"),
                                          e.SourceId),
        CommittedAt:                  e.CommittedAt.ToString("o"),
        DueAt:                        e.DueAt?.ToString("o"),
        Status:                       e.Status,
        Priority:                     MapPriority(e.Priority),
        BlockedBy:                    Deserialize(e.BlockedByJson),
        Blocks:                       Deserialize(e.BlocksJson),
        ImpactScore:                  e.ImpactScore,
        BurnoutContribution:          e.BurnoutContribution,
        LastActivity:                 e.LastActivity?.ToString("o"),
        OwnerDeliveryScoreAtCreation: e.OwnerDeliveryScoreAtCreation
    );

    private static string[] Deserialize(string json)
        => JsonSerializer.Deserialize<string[]>(json) ?? [];

    /// <summary>
    /// Maps C# enum name (Transcript/Chat/Email/Ado) to frontend source type string.
    /// </summary>
    private static string MapSourceType(string sourceType) => sourceType.ToLowerInvariant() switch
    {
        "transcript" => "meeting",
        "chat"       => "chat",
        "email"      => "email",
        "ado"        => "ado",
        _            => "meeting",
    };

    /// <summary>
    /// Maps storage priority string to frontend EisenhowerQuadrant string.
    /// EisenhowerScorer uses: urgent-important | schedule | delegate | defer
    /// Frontend expects:      urgent-important | not-urgent-important | urgent-not-important | not-urgent-not-important
    /// </summary>
    private static string MapPriority(string priority) => priority switch
    {
        "urgent-important"     => "urgent-important",
        "schedule"             => "not-urgent-important",
        "delegate"             => "urgent-not-important",
        "defer"                => "not-urgent-not-important",
        "not-urgent-important" => "not-urgent-important",
        "urgent-not-important" => "urgent-not-important",
        "not-urgent-not-important" => "not-urgent-not-important",
        _                      => "not-urgent-not-important",
    };
}

/// <summary>Source provenance DTO — matches frontend <c>CommitmentSource</c>.</summary>
public record CommitmentSourceDto(
    string Type,
    string Url,
    string Timestamp,
    string? SourceId
);
