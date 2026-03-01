using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Models.Agents;

namespace CommitApi.Agents;

/// <summary>
/// Fetches an ADO pull request diff and existing thread context,
/// then generates a structured review comment draft.
/// </summary>
public interface IPrReviewDrafter
{
    /// <summary>
    /// Generates a review comment draft for a given ADO PR.
    /// </summary>
    Task<AgentDraft?> DraftReviewAsync(
        string prId,
        string adoOrg,
        string adoProject,
        string adoRepo,
        string adoPat,
        string userId,
        CancellationToken ct = default);
}

public sealed class PrReviewDrafter : IPrReviewDrafter
{
    private readonly IHttpClientFactory         _http;
    private readonly ILogger<PrReviewDrafter>   _log;

    public PrReviewDrafter(IHttpClientFactory http, ILogger<PrReviewDrafter> log)
    {
        _http = http;
        _log  = log;
    }

    /// <inheritdoc />
    public async Task<AgentDraft?> DraftReviewAsync(
        string prId,
        string adoOrg,
        string adoProject,
        string adoRepo,
        string adoPat,
        string userId,
        CancellationToken ct = default)
    {
        // ── Fetch PR metadata + diff ───────────────────────────────────────────
        var client    = _http.CreateClient("ado");
        var authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{adoPat}"));

        var pr = await FetchPrAsync(client, authHeader, adoOrg, adoProject, adoRepo, prId, ct);
        if (pr is null) return null;

        var threads = await FetchThreadsAsync(client, authHeader, adoOrg, adoProject, adoRepo, prId, ct);
        var diff    = await FetchDiffSummaryAsync(client, authHeader, adoOrg, adoProject, adoRepo, prId, ct);

        // ── Generate structured draft ─────────────────────────────────────────
        var content = BuildReviewDraft(pr, threads, diff);

        _log.LogInformation("PrReviewDrafter: generated draft for PR {PrId} in {Org}/{Project}/{Repo}",
            prId, adoOrg, adoProject, adoRepo);

        return new AgentDraft(
            DraftId:        Guid.NewGuid().ToString(),
            ActionType:     "post-pr-comment",
            Content:        content,
            ContextSummary: $"PR #{prId} — {pr.Title}",
            Recipients:     [pr.CreatedBy],
            CreatedAt:      DateTimeOffset.UtcNow,
            Status:         "pending",
            EditedContent:  null);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static async Task<PrSummary?> FetchPrAsync(
        HttpClient client,
        string authHeader,
        string org, string project, string repo, string prId,
        CancellationToken ct)
    {
        var url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repo}/pullRequests/{prId}?api-version=7.1";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new PrSummary(
            root.GetProperty("pullRequestId").GetInt32().ToString(),
            root.TryGetProperty("title", out var t)     ? t.GetString() ?? "" : "",
            root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            root.TryGetProperty("createdBy", out var cb)
                && cb.TryGetProperty("uniqueName", out var un) ? un.GetString() ?? "" : "");
    }

    private static async Task<IReadOnlyList<string>> FetchThreadsAsync(
        HttpClient client,
        string authHeader,
        string org, string project, string repo, string prId,
        CancellationToken ct)
    {
        var url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repo}/pullRequests/{prId}/threads?api-version=7.1";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return [];

        var json  = await res.Content.ReadAsStringAsync(ct);
        using var doc   = JsonDocument.Parse(json);
        var threads = new List<string>();
        foreach (var thread in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            foreach (var comment in thread.GetProperty("comments").EnumerateArray())
            {
                if (comment.TryGetProperty("content", out var c) && c.GetString() is { } text && text.Length > 0)
                    threads.Add(text[..Math.Min(200, text.Length)]);
            }
        }
        return threads.Take(10).ToList();
    }

    private static async Task<string> FetchDiffSummaryAsync(
        HttpClient client,
        string authHeader,
        string org, string project, string repo, string prId,
        CancellationToken ct)
    {
        var url = $"https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repo}/pullRequests/{prId}/iterations?api-version=7.1";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return "(diff unavailable)";

        var json  = await res.Content.ReadAsStringAsync(ct);
        using var doc   = JsonDocument.Parse(json);
        var count = doc.RootElement.GetProperty("count").GetInt32();
        return $"{count} iteration(s) — full diff available via ADO link";
    }

    private static string BuildReviewDraft(
        PrSummary            pr,
        IReadOnlyList<string> threads,
        string               diffSummary)
    {
        var unresolvedNote = threads.Count > 0
            ? $"\n\n📋 Existing thread context ({threads.Count} comment(s) found):\n"
              + string.Join("\n", threads.Take(3).Select(t => $"  • {t}"))
            : "";

        return $"## Review — PR #{pr.Id}: {pr.Title}\n\n"
             + $"**Summary**: {(pr.Description.Length > 0 ? pr.Description[..Math.Min(200, pr.Description.Length)] : "No description provided.")}\n\n"
             + $"**Diff**: {diffSummary}"
             + unresolvedNote
             + "\n\n**Suggested review checklist**:\n"
             + "- [ ] Logic correctness\n"
             + "- [ ] Edge cases covered\n"
             + "- [ ] Tests added/updated\n"
             + "- [ ] No unintended side effects\n\n"
             + "_Draft generated by Commit. Edit before posting._";
    }

    private sealed record PrSummary(string Id, string Title, string Description, string CreatedBy);
}
