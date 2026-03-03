using Azure.Identity;
using CommitApi.Exceptions;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace CommitApi.Auth;

/// <summary>
/// Creates Microsoft Graph clients using the MSAL On-Behalf-Of (OBO) flow.
/// The incoming user bearer token is exchanged for a Graph-scoped OBO token,
/// so all Graph calls are made in the context of the actual user, not a service principal.
/// </summary>
public sealed class GraphClientFactory : IGraphClientFactory
{
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<GraphClientFactory> _logger;

    // Graph scopes required for Commit features.
    // Chat.ReadWrite + ChatMessage.Send require admin consent (delegated).
    private static readonly string[] GraphScopes =
    [
        "https://graph.microsoft.com/User.Read",
        "https://graph.microsoft.com/Chat.Read",
        "https://graph.microsoft.com/Chat.ReadWrite",
        "https://graph.microsoft.com/ChatMessage.Send",
        "https://graph.microsoft.com/Mail.Read",
        "https://graph.microsoft.com/Calendars.ReadWrite",
        "https://graph.microsoft.com/OnlineMeetings.Read",
        "https://graph.microsoft.com/Presence.Read",
    ];

    public GraphClientFactory(string tenantId, string clientId, string clientSecret,
        ILogger<GraphClientFactory> logger)
    {
        _tenantId     = tenantId;
        _clientId     = clientId;
        _clientSecret = clientSecret;
        _logger       = logger;
    }

    /// <inheritdoc />
    public GraphServiceClient CreateOnBehalfOf(string bearerToken)
    {
        // Exchange the incoming token for an OBO Graph token via MSAL
        var credential = new OnBehalfOfCredential(
            _tenantId,
            _clientId,
            _clientSecret,
            bearerToken);

        return new GraphServiceClient(credential, GraphScopes);
    }

    /// <inheritdoc />
    public async Task<(string DisplayName, string UserId)> GetCurrentUserAsync(
        string bearerToken, CancellationToken ct = default)
    {
        try
        {
            var client = CreateOnBehalfOf(bearerToken);
            var me = await client.Me.GetAsync(cancellationToken: ct);

            if (me is null)
                throw new AuthException("Graph /me returned null — token may be invalid");

            return (me.DisplayName ?? "Unknown User", me.Id ?? string.Empty);
        }
        catch (CommitException)
        {
            throw; // already typed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph OBO token exchange failed");
            throw new AuthException($"Failed to acquire OBO token: {ex.Message}");
        }
    }
}
