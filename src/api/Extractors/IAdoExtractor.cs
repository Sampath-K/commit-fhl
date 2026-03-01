using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Reads Azure DevOps PR review threads to surface commitment signals.
/// </summary>
public interface IAdoExtractor
{
    /// <summary>
    /// Returns raw commitments from unresolved review comments in
    /// open PRs assigned to or created by <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">AAD OID of the user (used to filter assigned PRs).</param>
    /// <param name="bearerToken">User bearer token (for display name resolution).</param>
    Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string userId,
        string bearerToken,
        CancellationToken ct = default);
}
