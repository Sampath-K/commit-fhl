using CommitApi.Auth;
using Microsoft.Graph.Models;

namespace CommitApi.Services;

/// <summary>
/// Sends Teams 1:1 chat messages via Microsoft Graph on behalf of the signed-in user.
///
/// Recipients are resolved from a hardcoded demo-tenant mapping first. Unresolved
/// names are skipped with a log warning rather than failing the whole request.
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
            ["marcus johnson"]  = "c1c0037d-1b8c-4c34-bede-f6dadc38a8c6",
            ["priya sharma"]    = "8d0832a0-c586-4c41-b6ad-02a76c5b326c",
            ["david park"]      = "6a638a9c-cad1-429d-8bc0-d435bf552e5a",
            ["alex chen"]       = "f7a02de7-e195-4894-bc23-f7f74b696cbd",
            ["sarah o'brien"]   = "5659f687-9ea8-4dfe-95c7-2990356288af",
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
        CancellationToken ct = default)
    {
        var client = _graphFactory.CreateOnBehalfOf(bearerToken);

        foreach (var name in recipientNames)
        {
            var recipientOid = ResolveOid(name);
            if (recipientOid is null)
            {
                _logger.LogWarning("Teams send: OID not found for recipient '{Name}' — skipping", name);
                continue;
            }

            if (string.Equals(recipientOid, senderUserId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Teams send: skipping self-message to '{Name}'", name);
                continue;
            }

            try
            {
                await SendToRecipientAsync(client, senderUserId, recipientOid, messageContent, ct);
                _logger.LogInformation("Teams message sent to '{Name}' ({Oid})", name, recipientOid);
            }
            catch (Exception ex)
            {
                // Non-fatal — log and continue with remaining recipients
                _logger.LogWarning(ex, "Teams send failed to '{Name}' ({Oid})", name, recipientOid);
            }
        }
    }

    private static string? ResolveOid(string displayName) =>
        DemoUserOids.TryGetValue(displayName.Trim(), out var oid) ? oid : null;

    private static async Task SendToRecipientAsync(
        Microsoft.Graph.GraphServiceClient client,
        string   senderOid,
        string   recipientOid,
        string   content,
        CancellationToken ct)
    {
        // Create (or retrieve existing) 1:1 chat between sender and recipient.
        // Graph SDK v5 (Kiota): @odata.type discriminator goes in AdditionalData.
        var chat = await client.Chats.PostAsync(new Chat
        {
            ChatType = ChatType.OneOnOne,
            Members  = new List<ConversationMember>
            {
                new AadUserConversationMember
                {
                    Roles = new List<string> { "owner" },
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["@odata.type"]    = "#microsoft.graph.aadUserConversationMember",
                        ["user@odata.bind"] =
                            $"https://graph.microsoft.com/v1.0/users('{senderOid}')",
                    },
                },
                new AadUserConversationMember
                {
                    Roles = new List<string> { "owner" },
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["@odata.type"]    = "#microsoft.graph.aadUserConversationMember",
                        ["user@odata.bind"] =
                            $"https://graph.microsoft.com/v1.0/users('{recipientOid}')",
                    },
                },
            },
        }, cancellationToken: ct);

        // Post the message
        await client.Chats[chat!.Id].Messages.PostAsync(new ChatMessage
        {
            Body = new ItemBody
            {
                Content     = content,
                ContentType = BodyType.Text,
            },
        }, cancellationToken: ct);
    }
}
