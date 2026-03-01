namespace CommitApi.Models.Agents;

/// <summary>
/// An AI-generated draft waiting for human approval.
/// Serialized to JSON and stored in CommitmentEntity.AgentDraftJson.
/// </summary>
public record AgentDraft(
    string   DraftId,
    string   ActionType,       // send-message | create-calendar-event | post-pr-comment | send-email
    string   Content,
    string   ContextSummary,
    string[] Recipients,
    DateTimeOffset CreatedAt,
    string   Status,           // pending | approved | edited | skipped
    string?  EditedContent);
