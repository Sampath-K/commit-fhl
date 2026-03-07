using System.Net;
using System.Text;
using CommitApi.Auth;
using Microsoft.Graph.Models;

namespace CommitApi.Services;

/// <summary>
/// Sends a Teams group chat message via Microsoft Graph on behalf of the signed-in user.
///
/// All resolved recipients plus the sender are added as members of a single group chat.
/// Unresolved names are skipped with a log warning rather than failing the whole request.
///
/// Admin consent required for delegated scopes:
///   Chat.ReadWrite, ChatMessage.Send
/// </summary>
public sealed class TeamsMessageSender : ITeamsMessageSender
{
    // Demo tenant (7k2cc2.onmicrosoft.com) display name → AAD OID mapping.
    // Used as the primary lookup so the demo works without User.ReadBasic.All scope.
    private static readonly Dictionary<string, string> DemoUserOids =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["marcus johnson"]   = "c1c0037d-1b8c-4c34-bede-f6dadc38a8c6",
            ["priya sharma"]     = "8d0832a0-c586-4c41-b6ad-02a76c5b326c",
            ["david park"]       = "6a638a9c-cad1-429d-8bc0-d435bf552e5a",
            ["alex chen"]        = "f7a02de7-e195-4894-bc23-f7f74b696cbd",
            ["sarah o'brien"]    = "5659f687-9ea8-4dfe-95c7-2990356288af",
            ["fatima al-rashid"] = "78a8c66f-2928-4edc-9230-d6a209e72f85",
        };

    private readonly IGraphClientFactory _graphFactory;
    private readonly ILogger<TeamsMessageSender> _logger;

    public TeamsMessageSender(IGraphClientFactory graphFactory, ILogger<TeamsMessageSender> logger)
    {
        _graphFactory = graphFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        string   bearerToken,
        string   senderUserId,
        string   messageContent,
        string[] recipientNames,
        string   topic,
        CancellationToken ct = default)
    {
        var client = _graphFactory.CreateOnBehalfOf(bearerToken);

        // Resolve all recipient OIDs, skipping unknowns and self
        var memberOids = new List<string> { senderUserId };
        foreach (var name in recipientNames)
        {
            var oid = ResolveOid(name);
            if (oid is null)
            {
                _logger.LogWarning("Teams send: OID not found for recipient '{Name}' — skipping", name);
                continue;
            }
            if (string.Equals(oid, senderUserId, StringComparison.OrdinalIgnoreCase))
                continue;

            memberOids.Add(oid);
        }

        if (memberOids.Count < 2)
        {
            _logger.LogWarning("Teams send: no resolvable recipients — message not sent");
            return;
        }

        var chatTopic = topic.Length > 50 ? string.Concat(topic.AsSpan(0, 47), "...") : topic;

        try
        {
            await SendGroupMessageAsync(client, memberOids, messageContent, chatTopic, ct);
            _logger.LogInformation("Teams group message sent to {Count} member(s)", memberOids.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Teams group message send failed");
        }
    }

    private static string? ResolveOid(string displayName) =>
        DemoUserOids.TryGetValue(displayName.Trim(), out var oid) ? oid : null;

    private static AadUserConversationMember MakeMember(string oid) =>
        new()
        {
            Roles = new List<string> { "owner" },
            AdditionalData = new Dictionary<string, object>
            {
                ["@odata.type"]     = "#microsoft.graph.aadUserConversationMember",
                ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{oid}')",
            },
        };

    private static async Task SendGroupMessageAsync(
        Microsoft.Graph.GraphServiceClient client,
        List<string> memberOids,
        string content,
        string topic,
        CancellationToken ct)
    {
        // Create a group chat with all members in one call.
        // Graph requires ChatType.Group when there are 3+ members.
        var chatType = memberOids.Count > 2 ? ChatType.Group : ChatType.OneOnOne;

        var chat = await client.Chats.PostAsync(new Chat
        {
            ChatType = chatType,
            Topic    = chatType == ChatType.Group ? topic : null,
            Members  = memberOids.Select(MakeMember).Cast<ConversationMember>().ToList(),
        }, cancellationToken: ct);

        await client.Chats[chat!.Id].Messages.PostAsync(new ChatMessage
        {
            Body = new ItemBody
            {
                Content     = PlainToHtml(content),
                ContentType = BodyType.Html,
            },
        }, cancellationToken: ct);
    }

    /// <summary>
    /// Converts the plain-text message format used by CascadeView into Teams-compatible HTML.
    ///
    /// Rules:
    ///   • Paragraphs separated by \n\n → &lt;p&gt; blocks
    ///   • Lines starting with "• "    → &lt;ul&gt;&lt;li&gt; list
    ///   • Lines starting with "N. "   → &lt;ol&gt;&lt;li&gt; list
    ///   • Line starting with "— "     → italicised signature
    ///   • Remaining \n within a para  → &lt;br&gt;
    /// </summary>
    internal static string PlainToHtml(string text)
    {
        var sb = new StringBuilder();

        foreach (var para in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines      = para.Split('\n');
            var textLines  = new List<string>();
            var listItems  = new List<(string text, bool ordered)>();

            foreach (var line in lines)
            {
                if (line.StartsWith("• "))
                    listItems.Add((line[2..], false));
                else if (line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.')
                    listItems.Add((line[2..].TrimStart(), true));
                else
                    textLines.Add(line);
            }

            // Text lines (non-bullet)
            if (textLines.Count > 0)
            {
                var joined = string.Join("<br>", textLines.Select(l =>
                    l.StartsWith("— ") || l.StartsWith("\u2014 ")
                        ? $"<em>{WebUtility.HtmlEncode(l)}</em>"
                        : WebUtility.HtmlEncode(l)));
                sb.Append("<p>").Append(joined).Append("</p>");
            }

            // List items
            if (listItems.Count > 0)
            {
                var isOrdered = listItems[0].ordered;
                var tag       = isOrdered ? "ol" : "ul";
                sb.Append('<').Append(tag).Append('>');
                foreach (var (itemText, _) in listItems)
                    sb.Append("<li>").Append(WebUtility.HtmlEncode(itemText)).Append("</li>");
                sb.Append("</").Append(tag).Append('>');
            }
        }

        return sb.ToString();
    }
}
