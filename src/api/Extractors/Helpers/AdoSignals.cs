namespace CommitApi.Extractors.Helpers;

/// <summary>
/// Signal detection helpers extracted from <see cref="AdoExtractor"/> for testability.
/// </summary>
internal static class AdoSignals
{
    private static readonly string[] ReviewSignals =
    [
        "please fix", "please update", "can you", "could you", "nitpick",
        "blocking", "needs to be", "should be", "must be", "action:", "todo:", "fixme:"
    ];

    internal static bool HasReviewSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ReviewSignals.Any(s => lower.Contains(s));
    }
}
