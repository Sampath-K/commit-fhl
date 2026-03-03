namespace CommitApi.Models.Agents;

/// <summary>
/// Payload for POST /api/v1/approvals.
/// Mirrors the frontend ApprovalDecision TypeScript type.
/// </summary>
public record ApprovalDecision(
    string    DraftId,
    string    CommitmentId,
    string    Decision,          // approve | edit | skip
    string?   EditedContent,
    string?   DraftContent,      // Original draft text (used for send-message actions)
    string?   DraftActionType,   // send-message | create-calendar-event | post-pr-comment | send-email
    string[]? DraftRecipients);  // Recipient display names
