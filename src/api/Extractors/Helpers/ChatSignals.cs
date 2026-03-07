namespace CommitApi.Extractors.Helpers;

/// <summary>
/// Signal detection helpers extracted from <see cref="ChatExtractor"/> for testability.
/// </summary>
internal static class ChatSignals
{
    private static readonly string[] ActionSignals =
    [
        "i'll", "i will", "will do", "will send", "will review", "will fix",
        "will submit", "will complete", "will share", "will update", "will provide",
        "will schedule", "will create", "will prepare", "will follow up", "will reach out",
        "by tomorrow", "by friday", "by eod", "by end of day",
        "by monday", "by next week", "by march", "by april", "by may", "by june",
        "by january", "by february", "by july", "by august", "by september",
        "by october", "by november", "by december",
        "by 1st", "by 2nd", "by 3rd", "by 4th", "by 5th", "by 6th", "by 7th",
        "by 8th", "by 9th", "by 10th", "by 15th", "by 20th", "by 25th", "by 30th",
        "i can", "let me", "i'll get", "on it", "taking this", "i own", "i'll own"
    ];

    internal static bool HasActionSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return ActionSignals.Any(signal => lower.Contains(signal));
    }

    internal static string InferTitle(string text)
    {
        var sentences = text.Split(['.', '!', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var raw = sentences.FirstOrDefault(s => HasActionSignal(s))
                  ?? sentences.FirstOrDefault()
                  ?? text.Trim();
        raw = raw.Trim();
        return raw.Length > 80 ? raw[..80] + "…" : raw;
    }

    internal static DateTimeOffset? InferDueDate(string text)
    {
        var lower = text.ToLowerInvariant();
        var now   = DateTimeOffset.UtcNow;
        if (lower.Contains("by eod") || lower.Contains("by end of day") || lower.Contains("today"))
            return now.Date.AddHours(18);
        if (lower.Contains("tomorrow"))
            return now.AddDays(1).Date.AddHours(18);
        if (lower.Contains("by friday") || lower.Contains("end of week"))
        {
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            return now.AddDays(daysUntilFriday).Date.AddHours(18);
        }
        if (lower.Contains("next week") || lower.Contains("by monday"))
            return now.AddDays(7 - (int)now.DayOfWeek + 1).Date.AddHours(9);

        var datePattern = System.Text.RegularExpressions.Regex.Match(lower,
            @"by (?:(\d{1,2})(?:st|nd|rd|th)?\s+(january|february|march|april|may|june|july|august|september|october|november|december)|" +
            @"(january|february|march|april|may|june|july|august|september|october|november|december)\s+(\d{1,2})(?:st|nd|rd|th)?)");
        if (datePattern.Success)
        {
            var monthNames = new[] { "", "january","february","march","april","may","june",
                                         "july","august","september","october","november","december" };
            int day, month;
            if (!string.IsNullOrEmpty(datePattern.Groups[1].Value))
            {
                day   = int.Parse(datePattern.Groups[1].Value);
                month = Array.IndexOf(monthNames, datePattern.Groups[2].Value);
            }
            else
            {
                month = Array.IndexOf(monthNames, datePattern.Groups[3].Value);
                day   = int.Parse(datePattern.Groups[4].Value);
            }
            if (month > 0 && day > 0)
            {
                var year = now.Year;
                var candidate = new DateTimeOffset(year, month, day, 18, 0, 0, TimeSpan.Zero);
                if (candidate < now) candidate = candidate.AddYears(1);
                return candidate;
            }
        }

        return null;
    }

    internal static string StripHtml(string html)
    {
        var stripped = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        return System.Text.RegularExpressions.Regex.Replace(stripped, @"\s+", " ").Trim();
    }
}
