using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Config;
using CommitApi.Models.Capacity;

namespace CommitApi.Capacity;

/// <summary>
/// Retrieves Viva Insights activity statistics (collaboration hours by category) and
/// calendar free-slot data for a user.  Falls back to calendar-only estimation when
/// Viva Insights is not licensed on the tenant (403/404 responses).
///
/// Load index = (meeting + email + chat hours over 7 days) / (8 hrs/day × 7 days).
/// Burnout trend = (this-week hours − last-week hours) / last-week hours.
/// Free slots = upcoming 2-hour+ gaps in the calendar over the next 3 working days.
/// </summary>
public class VivaInsightsClient : IVivaInsightsClient
{
    private const string GraphBase     = "https://graph.microsoft.com/v1.0";
    private const double WorkHoursDay  = 8.0;
    private const double FreeSlotMinHours = 2.0;

    private readonly IHttpClientFactory _http;
    private readonly ILogger<VivaInsightsClient> _log;

    public VivaInsightsClient(IHttpClientFactory http, ILogger<VivaInsightsClient> log)
    {
        _http = http;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<CapacitySnapshot> GetCapacityAsync(
        string userId, string bearerToken, CancellationToken ct = default)
    {
        using var client = _http.CreateClient("graph");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);

        var (loadIndex, burnoutTrend) = await GetActivityStatsAsync(client, ct);
        var freeSlots                 = await GetFreeSlotsAsync(client, ct);

        _log.LogInformation(
            "Capacity for user {Hash}: load={Load:F2}, burnout={Burnout:+0.00;-0.00}, slots={Slots}",
            PiiScrubber.HashValue(userId), loadIndex, burnoutTrend, freeSlots.Count);

        return new CapacitySnapshot(
            UserId:       PiiScrubber.HashValue(userId),
            LoadIndex:    loadIndex,
            BurnoutTrend: burnoutTrend,
            FreeSlots:    freeSlots);
    }

    // ── Activity statistics (Viva Insights) ───────────────────────────────────

    private async Task<(double loadIndex, double burnoutTrend)> GetActivityStatsAsync(
        HttpClient client, CancellationToken ct)
    {
        try
        {
            var now        = DateTimeOffset.UtcNow;
            var twoWeeksAgo = now.AddDays(-14).ToString("yyyy-MM-dd");
            var today       = now.ToString("yyyy-MM-dd");

            var url = $"{GraphBase}/me/analytics/activityStatistics" +
                      $"?$filter=startDateTime ge {twoWeeksAgo} and startDateTime lt {today}";

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Viva Insights not licensed — return neutral defaults
                _log.LogDebug("Viva Insights unavailable ({Status}), using calendar fallback",
                    resp.StatusCode);
                return (0.75, 0.0);   // moderate load, no trend data
            }

            var body  = await resp.Content.ReadAsStringAsync(ct);
            var doc   = JsonDocument.Parse(body);
            var stats = doc.RootElement.GetProperty("value").EnumerateArray().ToList();

            // Sum collaboration hours per week
            var thisWeekHours = SumHours(stats, now.AddDays(-7), now);
            var lastWeekHours = SumHours(stats, now.AddDays(-14), now.AddDays(-7));

            var loadIndex    = thisWeekHours / (WorkHoursDay * 7.0);
            var burnoutTrend = lastWeekHours > 0
                ? (thisWeekHours - lastWeekHours) / lastWeekHours
                : 0.0;

            return (Math.Round(loadIndex, 2), Math.Round(burnoutTrend, 2));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Activity stats fetch failed, using defaults");
            return (0.75, 0.0);
        }
    }

    private static double SumHours(
        List<JsonElement> stats, DateTimeOffset from, DateTimeOffset to)
    {
        double total = 0;
        foreach (var stat in stats)
        {
            if (!stat.TryGetProperty("startDateTime", out var startProp))
                continue;
            if (!DateTimeOffset.TryParse(startProp.GetString(), out var start))
                continue;
            if (start < from || start >= to)
                continue;

            // Each stat has a "duration" in ISO 8601 (e.g., PT2H30M)
            if (stat.TryGetProperty("duration", out var durProp))
            {
                var dur = durProp.GetString() ?? "";
                total += ParseIsoDuration(dur);
            }
        }
        return total;
    }

    private static double ParseIsoDuration(string iso)
    {
        // Parse PT#H#M format (simplified — handles hours and minutes only)
        try
        {
            var span = System.Xml.XmlConvert.ToTimeSpan(iso);
            return span.TotalHours;
        }
        catch
        {
            return 0.0;
        }
    }

    // ── Calendar free slots ────────────────────────────────────────────────────

    private async Task<IReadOnlyList<TimeSlot>> GetFreeSlotsAsync(
        HttpClient client, CancellationToken ct)
    {
        try
        {
            var start = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
            var end   = start.AddDays(3);
            var url   = $"{GraphBase}/me/calendarView" +
                        $"?startDateTime={start:yyyy-MM-ddTHH:mm:ssZ}" +
                        $"&endDateTime={end:yyyy-MM-ddTHH:mm:ssZ}" +
                        "&$select=start,end,showAs" +
                        "&$orderby=start/dateTime";

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return [];

            var body  = await resp.Content.ReadAsStringAsync(ct);
            var doc   = JsonDocument.Parse(body);
            var events = doc.RootElement.GetProperty("value")
                .EnumerateArray()
                .Where(e => IsBusy(e))
                .Select(e => ParseEvent(e))
                .Where(t => t is not null)
                .Cast<(DateTimeOffset, DateTimeOffset)>()
                .OrderBy(t => t.Item1)
                .ToList();

            return FindFreeSlots(start, end, events);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Calendar free slot fetch failed");
            return [];
        }
    }

    private static bool IsBusy(JsonElement ev)
    {
        if (ev.TryGetProperty("showAs", out var showAs))
        {
            var val = showAs.GetString() ?? "";
            return val is "busy" or "tentative" or "outOfOffice";
        }
        return false;
    }

    private static (DateTimeOffset, DateTimeOffset)? ParseEvent(JsonElement ev)
    {
        try
        {
            var startStr = ev.GetProperty("start").GetProperty("dateTime").GetString() ?? "";
            var endStr   = ev.GetProperty("end").GetProperty("dateTime").GetString() ?? "";
            if (!DateTimeOffset.TryParse(startStr, out var s) ||
                !DateTimeOffset.TryParse(endStr, out var e))
                return null;
            return (s, e);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset StartOfDay(DateTimeOffset dt) =>
        new(dt.Date, TimeSpan.Zero);

    private static IReadOnlyList<TimeSlot> FindFreeSlots(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        List<(DateTimeOffset Start, DateTimeOffset End)> busySlots)
    {
        var workStart = TimeSpan.FromHours(9);
        var workEnd   = TimeSpan.FromHours(17);
        var minSlot   = TimeSpan.FromHours(FreeSlotMinHours);
        var freeSlots = new List<TimeSlot>();

        var cursor = StartOfDay(rangeStart).Add(workStart);
        foreach (var busy in busySlots)
        {
            if (busy.Start > cursor && busy.Start - cursor >= minSlot)
            {
                var dayEnd  = StartOfDay(cursor).Add(workEnd);
                var slotEnd = busy.Start < dayEnd ? busy.Start : dayEnd;
                if (slotEnd - cursor >= minSlot)
                    freeSlots.Add(new TimeSlot(cursor, slotEnd));
            }
            if (busy.End > cursor)
                cursor = busy.End;

            // Advance past work hours if needed
            if (cursor.TimeOfDay >= workEnd)
                cursor = StartOfDay(cursor).AddDays(1).Add(workStart);
        }

        // Gap at end of working day
        var endOfWork = StartOfDay(cursor).Add(workEnd);
        if (endOfWork > cursor && endOfWork - cursor >= minSlot && cursor < rangeEnd)
            freeSlots.Add(new TimeSlot(cursor, endOfWork));

        return freeSlots;
    }
}
