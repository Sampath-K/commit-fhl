using CommitApi.Models.Feedback;

namespace CommitApi.Services;

/// <summary>
/// Derives a per-user signal profile from feedback history.
/// Results are cached for 5 minutes.
/// </summary>
public interface ISignalProfileService
{
    /// <summary>Returns the current signal profile for the given user.</summary>
    Task<UserSignalProfile> GetProfileAsync(string userId, CancellationToken ct = default);

    /// <summary>Invalidates the cached profile for the given user (call after recording feedback).</summary>
    void InvalidateCache(string userId);
}
