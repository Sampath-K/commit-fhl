namespace CommitApi.Extractors.Helpers;

/// <summary>
/// Signal detection helpers extracted from <see cref="EmailExtractor"/> for testability.
/// </summary>
internal static class EmailSignals
{
    private static readonly string[] ActionSubjectPrefixes =
        ["action required", "please review", "fyi:", "re:", "follow up", "reminder"];

    private static readonly string[] ActionBodySignals =
        ["please", "can you", "could you", "action item", "follow up", "by when", "deadline"];

    internal static bool HasActionSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ActionSubjectPrefixes.Any(p => lower.StartsWith(p))
            || ActionBodySignals.Any(s => lower.Contains(s));
    }

    internal static string NormalizeSubject(string subject)
    {
        var clean = System.Text.RegularExpressions.Regex.Replace(
            subject, @"^(Re:|Fwd?:|FW:|RE:|FWD?:)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        return clean.Length > 100 ? clean[..100] + "…" : clean;
    }

    internal static DateTimeOffset? InferDueDate(string text)
    {
        var lower = text.ToLowerInvariant();
        var now   = DateTimeOffset.UtcNow;
        if (lower.Contains("by eod") || lower.Contains("today"))   return now.Date.AddHours(18);
        if (lower.Contains("tomorrow"))                             return now.AddDays(1).Date.AddHours(18);
        if (lower.Contains("by friday") || lower.Contains("end of week"))
        {
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(daysUntilFriday).Date.AddHours(18);
        }
        if (lower.Contains("next week") || lower.Contains("by monday"))
            return now.AddDays(7 - (int)now.DayOfWeek + 1).Date.AddHours(9);
        return null;
    }
}
