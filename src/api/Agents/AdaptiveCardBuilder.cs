using System.Text.Json;
using CommitApi.Models.Agents;

namespace CommitApi.Agents;

/// <summary>
/// Builds Adaptive Card JSON payloads for agent drafts.
/// Cards render in Teams desktop, web, and mobile (Adaptive Cards v1.5).
/// </summary>
public static class AdaptiveCardBuilder
{
    /// <summary>
    /// Creates an Adaptive Card for a pending agent draft.
    /// Includes a context strip, draft content, and Approve / Edit / Skip buttons.
    /// </summary>
    public static string BuildDraftCard(AgentDraft draft, string commitmentId)
    {
        var actionTypeLabel = draft.ActionType switch
        {
            "send-message"          => "📨 Teams Message",
            "create-calendar-event" => "📅 Calendar Block",
            "post-pr-comment"       => "💬 PR Comment",
            "send-email"            => "✉️ Email",
            _                       => "Agent Draft",
        };

        var recipientList = draft.Recipients.Length > 0
            ? string.Join(", ", draft.Recipients.Take(3))
                + (draft.Recipients.Length > 3 ? $" +{draft.Recipients.Length - 3} more" : "")
            : "No recipients";

        var card = new
        {
            type    = "AdaptiveCard",
            version = "1.5",
            body    = new object[]
            {
                // ── Header ──────────────────────────────────────────────────
                new
                {
                    type     = "ColumnSet",
                    columns  = new object[]
                    {
                        new
                        {
                            type  = "Column",
                            width = "stretch",
                            items = new object[]
                            {
                                new { type = "TextBlock", text = actionTypeLabel, weight = "bolder", size = "medium" },
                                new { type = "TextBlock", text = recipientList, isSubtle = true, size = "small", wrap = true },
                            }
                        }
                    }
                },
                // ── Context strip ───────────────────────────────────────────
                new
                {
                    type            = "Container",
                    style           = "emphasis",
                    bleed           = false,
                    items           = new object[]
                    {
                        new { type = "TextBlock", text = "Context", weight = "bolder", size = "small" },
                        new { type = "TextBlock", text = draft.ContextSummary, wrap = true, size = "small" },
                    }
                },
                // ── Draft content ────────────────────────────────────────────
                new
                {
                    type  = "TextBlock",
                    text  = draft.Content,
                    wrap  = true,
                    style = "default",
                },
            },
            actions = new object[]
            {
                new
                {
                    type  = "Action.Http",
                    title = "✓ Approve",
                    method = "POST",
                    url   = "/api/v1/approvals",
                    body  = JsonSerializer.Serialize(new
                    {
                        draftId      = draft.DraftId,
                        commitmentId,
                        decision     = "approve",
                    }),
                    style = "positive",
                },
                new
                {
                    type    = "Action.ShowCard",
                    title   = "✎ Edit",
                    card    = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new { type = "Input.Text", id = "editedContent", value = draft.Content, isMultiline = true, label = "Edit draft" },
                        },
                        actions = new object[]
                        {
                            new
                            {
                                type  = "Action.Http",
                                title = "Send edited version",
                                method = "POST",
                                url   = "/api/v1/approvals",
                                body  = $"{{\"draftId\":\"{draft.DraftId}\",\"commitmentId\":\"{commitmentId}\",\"decision\":\"edit\",\"editedContent\":\"{{{{editedContent.value}}}}\"}}",
                            }
                        }
                    }
                },
                new
                {
                    type   = "Action.Http",
                    title  = "⏭ Skip — I'll handle it",
                    method = "POST",
                    url    = "/api/v1/approvals",
                    body   = JsonSerializer.Serialize(new
                    {
                        draftId      = draft.DraftId,
                        commitmentId,
                        decision     = "skip",
                    }),
                    style = "destructive",
                },
            },
            msteams = new
            {
                type   = "messageBack",
                displayText = "Agent draft pending your review",
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Creates a simple informational card (e.g., overcommit warning, calendar suggestion).
    /// </summary>
    public static string BuildInfoCard(string title, string body, string? ctaLabel = null, string? ctaUrl = null)
    {
        var actions = ctaLabel is not null && ctaUrl is not null
            ? new object[] { new { type = "Action.OpenUrl", title = ctaLabel, url = ctaUrl } }
            : Array.Empty<object>();

        var card = new
        {
            type    = "AdaptiveCard",
            version = "1.5",
            body    = new object[]
            {
                new { type = "TextBlock", text = title, weight = "bolder", size = "medium", wrap = true },
                new { type = "TextBlock", text = body,  wrap  = true },
            },
            actions,
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = false });
    }
}
