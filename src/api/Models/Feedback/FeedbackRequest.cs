using System.ComponentModel.DataAnnotations;

namespace CommitApi.Models.Feedback;

/// <summary>
/// Request body for the POST /api/v1/commitments/{id}/feedback endpoint.
/// No raw text is stored — only hashed fingerprints (GDPR compliant).
/// </summary>
public record FeedbackRequest(
    string CommitmentId,
    FeedbackType Type,

    /// <summary>Optional comment — max 200 chars, not stored as-is.</summary>
    [MaxLength(200)] string? Comment = null
);
