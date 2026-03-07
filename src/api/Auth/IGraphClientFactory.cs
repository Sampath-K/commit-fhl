using Microsoft.Graph;

namespace CommitApi.Auth;

/// <summary>
/// Factory for creating authenticated Microsoft Graph clients.
/// Uses MSAL On-Behalf-Of (OBO) flow — the user's access token is exchanged
/// for a Graph-scoped token on their behalf.
/// </summary>
public interface IGraphClientFactory
{
    /// <summary>
    /// Creates a GraphServiceClient authenticated as the calling user via OBO.
    /// </summary>
    /// <param name="bearerToken">The user's incoming bearer token (from Authorization header).</param>
    /// <returns>A GraphServiceClient ready to make calls on behalf of the user.</returns>
    /// <exception cref="CommitApi.Exceptions.AuthException">
    /// Thrown if the OBO token exchange fails or the token is invalid.
    /// </exception>
    GraphServiceClient CreateOnBehalfOf(string bearerToken);

    /// <summary>
    /// Exchanges the user's bearer token for a Graph-scoped OBO access token string.
    /// Use this to get a token suitable for direct HttpClient calls to Graph APIs.
    /// </summary>
    /// <param name="bearerToken">The user's incoming bearer token (Teams SSO or app-scoped).</param>
    /// <returns>Access token with audience https://graph.microsoft.com.</returns>
    Task<string> GetOboTokenAsync(string bearerToken, CancellationToken ct = default);

    /// <summary>
    /// Resolves the display name and AAD Object ID for the token owner by calling /me.
    /// Used for the health check and to validate Graph connectivity.
    /// </summary>
    /// <param name="bearerToken">The user's incoming bearer token.</param>
    /// <returns>Tuple of (displayName, userId).</returns>
    Task<(string DisplayName, string UserId)> GetCurrentUserAsync(string bearerToken,
        CancellationToken ct = default);
}
