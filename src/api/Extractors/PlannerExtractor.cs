using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Models.Extraction;

namespace CommitApi.Extractors;

/// <summary>
/// Extracts commitments from active Microsoft Planner tasks assigned to the user.
/// Loop tasks sync to Planner, so this captures Loop commitments as well.
///
/// Only tasks with a due date that were created within the look-back window are included.
/// Confidence is fixed at 0.85 — Planner tasks are explicit commitments, not inferred signals.
///
/// Required Graph scope: Tasks.Read
/// </summary>
public sealed class PlannerExtractor : IPlannerExtractor
{
    private readonly ILogger<PlannerExtractor> _logger;
    private readonly HttpClient _http;

    private const string GraphV1 = "https://graph.microsoft.com/v1.0";

    public PlannerExtractor(
        ILogger<PlannerExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _http   = httpClientFactory.CreateClient("graph");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RawCommitment>> ExtractAsync(
        string bearerToken,
        int days = 7,
        CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var commitments = new List<RawCommitment>();

        // Fetch all active (incomplete) tasks assigned to the current user
        var tasksUrl = $"{GraphV1}/me/planner/tasks" +
                       "?$select=title,dueDateTime,createdDateTime,completedDateTime,assignments,percentComplete,planId,id";

        var tasks = await GetPagedAsync(tasksUrl, bearerToken, ct);

        // Cache planId → plan title to avoid duplicate Graph calls
        var planTitleCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var task in tasks)
        {
            var pct = task.TryGetProperty("percentComplete", out var pc) ? pc.GetInt32() : 0;

            // Completed tasks (percentComplete == 100) → yield as completions if recently done
            if (pct >= 100)
            {
                if (!task.TryGetProperty("completedDateTime", out var completedProp)) continue;
                if (!DateTimeOffset.TryParse(completedProp.GetString(), out var completedAt)) continue;
                if (completedAt < since) continue;

                var cTitle  = task.TryGetProperty("title", out var ct2) ? ct2.GetString() ?? "Planner task" : "Planner task";
                var cTaskId = task.TryGetProperty("id", out var ctid) ? ctid.GetString() : null;
                var cPlanId = task.TryGetProperty("planId", out var cpid) ? cpid.GetString() : null;

                string? cPlanTitle = null;
                if (cPlanId is not null)
                {
                    if (planTitleCache.TryGetValue(cPlanId, out var cached2)) cPlanTitle = string.IsNullOrEmpty(cached2) ? null : cached2;
                    else { cPlanTitle = await FetchPlanTitleAsync(cPlanId, bearerToken, ct); planTitleCache[cPlanId] = cPlanTitle ?? ""; }
                }

                var cWebUrl = cTaskId is not null
                    ? $"https://tasks.office.com/en-US/Home/Planner#/taskdetail/{cTaskId}"
                    : "https://tasks.office.com";
                var cTruncated = cTitle.Length > 80 ? cTitle[..80] + "…" : cTitle;

                commitments.Add(new RawCommitment(
                    Title:            $"Completed: {cTruncated}",
                    OwnerUserId:      "self",
                    OwnerDisplayName: "Me",
                    SourceType:       CommitmentSourceType.Planner,
                    SourceUrl:        cWebUrl,
                    ExtractedAt:      completedAt,
                    DueAt:            null,
                    Confidence:       1.0,
                    WatcherUserIds:   [],
                    SourceContext:    $"Planner task completed {completedAt:MMM d}",
                    ProjectContext:   cPlanTitle,
                    ArtifactName:     null,
                    ItemKind:         ItemKind.Completion));
                continue;
            }

            // Incomplete tasks → commitments (must have due date + created within look-back)
            if (!task.TryGetProperty("dueDateTime", out var dueProp)) continue;
            if (!DateTimeOffset.TryParse(dueProp.GetString(), out var dueAt)) continue;

            // Must have been created within the look-back window
            if (!task.TryGetProperty("createdDateTime", out var createdProp)) continue;
            if (!DateTimeOffset.TryParse(createdProp.GetString(), out var createdAt)) continue;
            if (createdAt < since) continue;

            var title  = task.TryGetProperty("title", out var t) ? t.GetString() ?? "Planner task" : "Planner task";
            var taskId = task.TryGetProperty("id", out var tid) ? tid.GetString() : null;
            var planId = task.TryGetProperty("planId", out var pid) ? pid.GetString() : null;

            // Resolve plan title for ProjectContext (lazy, cached)
            string? planTitle = null;
            if (planId is not null)
            {
                if (planTitleCache.TryGetValue(planId, out var cached))
                {
                    planTitle = string.IsNullOrEmpty(cached) ? null : cached;
                }
                else
                {
                    planTitle = await FetchPlanTitleAsync(planId, bearerToken, ct);
                    planTitleCache[planId] = planTitle ?? "";
                }
            }

            // Build a deep-link URL using the Planner task ID
            var webUrl = taskId is not null
                ? $"https://tasks.office.com/en-US/Home/Planner#/taskdetail/{taskId}"
                : "https://tasks.office.com";

            var truncatedTitle = title.Length > 80 ? title[..80] + "…" : title;

            commitments.Add(new RawCommitment(
                Title:            truncatedTitle,
                OwnerUserId:      "self",       // /me/planner/tasks are always assigned to the caller
                OwnerDisplayName: "Me",
                SourceType:       CommitmentSourceType.Planner,
                SourceUrl:        webUrl,
                ExtractedAt:      DateTimeOffset.UtcNow,
                DueAt:            dueAt,
                Confidence:       0.85,         // Explicit task = high confidence commitment
                WatcherUserIds:   [],
                SourceContext:    $"Planner task due {dueAt:MMM d}",
                ProjectContext:   planTitle,
                ArtifactName:     null));   // task title is already the commitment title
        }

        _logger.LogInformation("PlannerExtractor: {Count} raw commitments from last {Days}d", commitments.Count, days);
        return commitments;
    }

    /// <summary>Fetches the title of a Planner plan by its ID.</summary>
    private async Task<string?> FetchPlanTitleAsync(string planId, string bearerToken, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{GraphV1}/planner/plans/{planId}?$select=title");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<JsonElement>> GetPagedAsync(
        string url,
        string bearerToken,
        CancellationToken ct)
    {
        var results = new List<JsonElement>();
        string? nextLink = url;

        while (nextLink is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, nextLink);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await _http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode) break;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var value))
                foreach (var item in value.EnumerateArray())
                    results.Add(item.Clone());

            nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? nl.GetString()
                : null;
        }

        return results;
    }
}
