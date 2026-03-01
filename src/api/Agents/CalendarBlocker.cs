using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CommitApi.Auth;
using CommitApi.Capacity;
using CommitApi.Models.Capacity;

namespace CommitApi.Agents;

/// <summary>
/// Finds the next available 2-hour focus block and creates a calendar event
/// via Microsoft Graph POST /me/events.
/// </summary>
public interface ICalendarBlocker
{
    /// <summary>
    /// Creates a 2-hour "Focus Time — Commit" calendar event in the next available slot.
    /// Returns the created event ID, or <c>null</c> if no slot is available.
    /// </summary>
    Task<string?> BlockFocusTimeAsync(string userId, string bearerToken, string taskTitle, CancellationToken ct = default);
}

public sealed class CalendarBlocker : ICalendarBlocker
{
    private readonly IVivaInsightsClient        _viva;
    private readonly IHttpClientFactory         _http;
    private readonly ILogger<CalendarBlocker>   _log;

    public CalendarBlocker(
        IVivaInsightsClient viva,
        IHttpClientFactory http,
        ILogger<CalendarBlocker> log)
    {
        _viva = viva;
        _http = http;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<string?> BlockFocusTimeAsync(
        string userId,
        string bearerToken,
        string taskTitle,
        CancellationToken ct = default)
    {
        // ── Find next free slot ────────────────────────────────────────────────
        var snapshot = await _viva.GetCapacityAsync(userId, bearerToken, ct);

        var slot = snapshot.FreeSlots
            .FirstOrDefault(s => (s.End - s.Start).TotalHours >= 2);

        if (slot is null)
        {
            _log.LogWarning("CalendarBlocker: no 2-hour free slot found for user {UserId}", userId);
            return null;
        }

        // ── POST /me/events ────────────────────────────────────────────────────
        var eventBody = new
        {
            subject = $"🔒 Focus Time — {taskTitle}",
            body    = new
            {
                contentType = "HTML",
                content     = "<p>Blocked by <strong>Commit</strong> for focused work. "
                            + "Cancel if no longer needed.</p>"
            },
            start = new { dateTime = slot.Start.ToString("o"), timeZone = "UTC" },
            end   = new
            {
                dateTime = slot.Start.AddHours(2).ToString("o"),
                timeZone = "UTC"
            },
            showAs         = "busy",
            isReminderOn   = false,
            categories     = new[] { "Commit — Focus" },
        };

        var client  = _http.CreateClient("graph");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(eventBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("CalendarBlocker: Graph event creation failed — {Status}", response.StatusCode);
            return null;
        }

        var json    = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var eventId = doc.RootElement.GetProperty("id").GetString();

        _log.LogInformation("CalendarBlocker: created focus event {EventId} for user {UserId} at {Start}",
            eventId, userId, slot.Start);

        return eventId;
    }
}
