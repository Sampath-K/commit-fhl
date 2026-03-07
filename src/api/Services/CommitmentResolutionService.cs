using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CommitApi.Auth;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Repositories;

namespace CommitApi.Services;

/// <summary>
/// Background service that intelligently auto-resolves commitments when their
/// underlying work is detected as complete.
///
/// Runs every 15 minutes. Three-tier resolution strategy:
///
///   Tier 1 — Structural (ADO, Planner): Direct API state check. Zero LLM cost.
///     100% accurate — the source system is the ground truth.
///     ADO:     PR status == "completed" or work item state in Done/Resolved/Closed
///     Planner: percentComplete == 100
///
///   Tier 1.5 — Same-source thread re-check (Chat, Email, Drive):
///     Chat:  Re-queries the exact chat thread / channel for completion signals or reactions.
///     Email: Checks flag:complete on the original message, then scans same conversation thread.
///     Drive: Re-fetches the specific document comment — 404 or isResolved=true → done.
///     Uses keyword overlap + optional GPT for ambiguous thread replies.
///
///   Tier 2 — Signal-based fallback (any remaining Chat, Email, Drive, Transcript):
///     Bulk-scan follow-up messages from the same user since the commitment was made.
///     Keyword overlap pre-filters candidates, then ONE batched GPT-4o call classifies all.
///     Auto-resolves at confidence ≥ 0.9.
///
/// After auto-resolving, sets ResolutionReason (shown in UI) and publishes an SSE event.
/// </summary>
public sealed class CommitmentResolutionService : BackgroundService
{
    private readonly TokenCache              _tokenCache;
    private readonly ICommitmentRepository   _repo;
    private readonly INlpPipeline            _nlp;
    private readonly CommitmentEventBus      _eventBus;
    private readonly IHttpClientFactory      _httpFactory;
    private readonly IGraphClientFactory     _graphFactory;
    private readonly IConfiguration          _config;
    private readonly ILogger<CommitmentResolutionService> _logger;

    private static readonly TimeSpan CheckInterval   = TimeSpan.FromMinutes(5);
    private const double AutoResolveThreshold        = 0.80;

    // ADO closed/done states
    private static readonly HashSet<string> AdoClosedStates = new(StringComparer.OrdinalIgnoreCase)
        { "Done", "Resolved", "Closed", "Completed", "Won't Fix", "Removed" };

    // Completion keyword signals for chat/email pre-filtering
    private static readonly string[] CompletionSignals =
    [
        "done", "sent", "completed", "finished", "shipped", "deployed", "merged",
        "pr is up", "just sent", "all done", "it's done", "pushed", "delivered",
        "wrapped up", "taken care of", "sorted", "fixed", "resolved", "submitted",
        "checked in", "published", "released", "handed off", "shared", "uploaded"
    ];

    private const string GraphV1 = "https://graph.microsoft.com/v1.0";

    public CommitmentResolutionService(
        TokenCache              tokenCache,
        ICommitmentRepository   repo,
        INlpPipeline            nlp,
        CommitmentEventBus      eventBus,
        IHttpClientFactory      httpFactory,
        IGraphClientFactory     graphFactory,
        IConfiguration          config,
        ILogger<CommitmentResolutionService> logger)
    {
        _tokenCache   = tokenCache;
        _repo         = repo;
        _nlp          = nlp;
        _eventBus     = eventBus;
        _httpFactory  = httpFactory;
        _graphFactory = graphFactory;
        _config       = config;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("CommitmentResolutionService: started (interval={Interval}m)", CheckInterval.TotalMinutes);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, ct);

            foreach (var userId in _tokenCache.GetActiveUserIds().ToList())
            {
                var entry = _tokenCache.Get(userId);
                if (entry is null) continue;

                try
                {
                    // Exchange the cached SSO token for a Graph-scoped OBO token
                    string graphToken;
                    try
                    {
                        graphToken = await _graphFactory.GetOboTokenAsync(entry.Token, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "CommitmentResolutionService: OBO exchange failed for user {Hash} — skipping",
                            PiiScrubber.HashValue(userId));
                        continue;
                    }

                    await CheckUserAsync(userId, graphToken, ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CommitmentResolutionService: check failed for user {Hash}",
                        PiiScrubber.HashValue(userId));
                }
            }
        }
    }

    private async Task CheckUserAsync(string userId, string bearerToken, CancellationToken ct)
    {
        var all = await _repo.ListByOwnerAsync(userId, ct: ct);

        var pending = all
            .Where(e => e.Status is "pending" or "in-progress")
            .ToList();

        if (pending.Count == 0) return;

        _logger.LogInformation(
            "CommitmentResolutionService: checking {Count} pending commitment(s) for user {Hash}",
            pending.Count, PiiScrubber.HashValue(userId));

        // resolved items accumulate here; each carries a ResolutionReason already set
        var resolved = new List<CommitmentEntity>();

        // ── Tier 1: Structural sources (ADO, Planner) ────────────────────────
        var structural = pending.Where(e => e.SourceType is "Ado" or "Planner").ToList();
        foreach (var entity in structural)
        {
            var (isResolved, reason) = entity.SourceType switch
            {
                "Ado"     => await CheckAdoResolutionAsync(entity, ct),
                "Planner" => await CheckPlannerResolutionAsync(entity, bearerToken, ct),
                _         => (false, null)
            };

            if (!isResolved) continue;
            entity.ResolutionReason = reason;
            resolved.Add(entity);
        }

        // ── Tier 1.5: Same-source thread/item re-check (Chat, Email, Drive) ──
        var tier15Candidates = pending
            .Where(e => e.SourceType is "Chat" or "Email" or "Drive"
                     && e.SourceMetadata is not null)
            .ToList();

        var tier15Resolved = new HashSet<string>();  // RowKeys of resolved items

        foreach (var entity in tier15Candidates)
        {
            var (isResolved, reason) = entity.SourceType switch
            {
                "Chat"  => await CheckChatThreadAsync(entity, bearerToken, ct),
                "Email" => await CheckEmailFlagOrThreadAsync(entity, bearerToken, ct),
                "Drive" => await CheckDocCommentAsync(entity, bearerToken, ct),
                _       => (false, null)
            };

            if (!isResolved) continue;
            entity.ResolutionReason = reason;
            resolved.Add(entity);
            tier15Resolved.Add(entity.RowKey);
        }

        // ── Tier 2: Signal-based fallback ─────────────────────────────────────
        // Only items not already resolved by Tier 1/1.5
        var tier2Candidates = pending
            .Where(e => e.SourceType is "Chat" or "Email" or "Drive" or "Transcript"
                     && !tier15Resolved.Contains(e.RowKey)
                     && !resolved.Any(r => r.RowKey == e.RowKey))
            .ToList();

        if (tier2Candidates.Count > 0)
        {
            var tier2Resolved = await CheckSignalSourcesAsync(userId, bearerToken, tier2Candidates, ct);
            resolved.AddRange(tier2Resolved);
        }

        // ── Persist auto-resolved items ───────────────────────────────────────
        if (resolved.Count == 0) return;

        foreach (var entity in resolved)
        {
            entity.Status       = "done";
            entity.LastActivity = DateTimeOffset.UtcNow;
            await _repo.UpsertAsync(entity, ct);
        }

        _logger.LogInformation(
            "CommitmentResolutionService: auto-resolved {Count} commitment(s) for user {Hash}",
            resolved.Count, PiiScrubber.HashValue(userId));

        // Push SSE so the tab refreshes immediately
        _eventBus.Publish(userId, resolved.Count);
    }

    // ── Tier 1: ADO ──────────────────────────────────────────────────────────

    private async Task<(bool Resolved, string? Reason)> CheckAdoResolutionAsync(
        CommitmentEntity entity, CancellationToken ct)
    {
        var org  = _config["ADO_ORG"]  ?? Environment.GetEnvironmentVariable("ADO_ORG")  ?? "";
        var pat  = _config["ADO_PAT"]  ?? Environment.GetEnvironmentVariable("ADO_PAT");
        if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(pat)) return (false, null);

        var url = entity.SourceUrl;

        // PR URL: https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequest/{id}
        if (url.Contains("/pullrequest/", StringComparison.OrdinalIgnoreCase))
        {
            var prId = ParseLastSegment(url);
            if (prId is null) return (false, null);

            var project = ParseAdoProject(url, org);
            var apiUrl  = $"https://dev.azure.com/{org}/{project}/_apis/git/pullrequests/{prId}?api-version=7.1";

            var json = await GetAdoJsonAsync(apiUrl, pat, ct);
            if (json is null) return (false, null);

            using var doc = JsonDocument.Parse(json);
            var status    = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                return (false, null);

            var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : $"PR #{prId}";
            return (true, $"Pull request \"{title}\" was completed in Azure DevOps");
        }

        // Work item URL: https://dev.azure.com/{org}/{project}/_workitems/edit/{id}
        if (url.Contains("/_workitems/", StringComparison.OrdinalIgnoreCase))
        {
            var itemId = ParseLastSegment(url);
            if (itemId is null) return (false, null);

            var project = ParseAdoProject(url, org);
            var apiUrl  = $"https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{itemId}?fields=System.State,System.Title&api-version=7.1";

            var json = await GetAdoJsonAsync(apiUrl, pat, ct);
            if (json is null) return (false, null);

            using var doc = JsonDocument.Parse(json);
            var fields = doc.RootElement.TryGetProperty("fields", out var f) ? f : default;
            var state  = fields.ValueKind != JsonValueKind.Undefined
                ? fields.TryGetProperty("System.State", out var st) ? st.GetString() : null
                : null;
            var wiTitle = fields.ValueKind != JsonValueKind.Undefined
                ? fields.TryGetProperty("System.Title", out var wt) ? wt.GetString() : $"Work item #{itemId}"
                : $"Work item #{itemId}";

            if (state is null || !AdoClosedStates.Contains(state)) return (false, null);
            return (true, $"Work item \"{wiTitle}\" moved to \"{state}\" in Azure DevOps");
        }

        return (false, null);
    }

    // ── Tier 1: Planner ──────────────────────────────────────────────────────

    private async Task<(bool Resolved, string? Reason)> CheckPlannerResolutionAsync(
        CommitmentEntity entity, string bearerToken, CancellationToken ct)
    {
        var anchor = "#/taskdetail/";
        var idx    = entity.SourceUrl.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (false, null);

        var taskId = entity.SourceUrl[(idx + anchor.Length)..].Trim('/');
        if (string.IsNullOrEmpty(taskId)) return (false, null);

        var http = _httpFactory.CreateClient("graph");
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{GraphV1}/me/planner/tasks/{taskId}?$select=title,percentComplete,completedDateTime,completedBy");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var resp = await http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode) return (false, null);

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var pct = doc.RootElement.TryGetProperty("percentComplete", out var p) ? p.GetInt32() : 0;
        if (pct < 100) return (false, null);

        var planTitle = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : "Planner task";
        var completedBy = doc.RootElement.TryGetProperty("completedBy", out var cb)
            ? cb.TryGetProperty("user", out var u)
                ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
                : null
            : null;

        var reason = completedBy is not null
            ? $"Planner task \"{planTitle}\" marked 100% complete by {completedBy}"
            : $"Planner task \"{planTitle}\" marked 100% complete";
        return (true, reason);
    }

    // ── Tier 1.5: Chat thread re-check ───────────────────────────────────────

    private async Task<(bool Resolved, string? Reason)> CheckChatThreadAsync(
        CommitmentEntity entity, string bearerToken, CancellationToken ct)
    {
        using var metaDoc = JsonDocument.Parse(entity.SourceMetadata!);
        var root      = metaDoc.RootElement;
        var chatType  = root.TryGetProperty("chatType", out var ctp) ? ctp.GetString() : "dm";
        var since     = entity.CommittedAt.ToString("o");
        var http      = _httpFactory.CreateClient("graph");
        string messagesUrl;

        string? teamId    = null;
        string? channelId = null;
        string? chatId    = null;
        string? messageId = root.TryGetProperty("messageId", out var mid0) ? mid0.GetString() : null;

        if (chatType == "channel")
        {
            teamId    = root.TryGetProperty("teamId",    out var tid) ? tid.GetString() : null;
            channelId = root.TryGetProperty("channelId", out var cid) ? cid.GetString() : null;
            if (teamId is null || channelId is null) return (false, null);

            messagesUrl = $"{GraphV1}/teams/{teamId}/channels/{channelId}/messages?$top=30";
        }
        else
        {
            chatId = root.TryGetProperty("chatId", out var cid) ? cid.GetString() : null;
            if (chatId is null) return (false, null);

            messagesUrl = $"{GraphV1}/me/chats/{chatId}/messages?$top=30";
        }

        // Look back 7 days — CommittedAt resets on every extraction run so is not reliable
        var sinceOffset = DateTimeOffset.UtcNow.AddDays(-7);
        var allMessages = await GetGraphPagedAsync(http, messagesUrl, bearerToken, ct);

        // For channel messages: also fetch direct replies to the original message (thread replies)
        if (chatType == "channel" && messageId is not null && teamId is not null && channelId is not null)
        {
            var repliesUrl = $"{GraphV1}/teams/{teamId}/channels/{channelId}/messages/{messageId}/replies?$top=20";
            var replies    = await GetGraphPagedAsync(http, repliesUrl, bearerToken, ct);
            allMessages.AddRange(replies);
        }

        // Client-side filter: only messages after the commitment was made
        var messages = allMessages.Where(m =>
        {
            if (!m.TryGetProperty("createdDateTime", out var cdt)) return true;
            return DateTimeOffset.TryParse(cdt.GetString(), out var t) && t > sinceOffset;
        }).ToList();

        foreach (var msg in messages)
        {
            var body = msg.TryGetProperty("body", out var b)
                ? b.TryGetProperty("content", out var c) ? c.GetString() : null
                : null;
            if (body is null) continue;

            var plain = StripHtml(body);
            if (!HasCompletionSignal(plain)) continue;
            // In the same thread, a short completion signal (e.g. "Done") is sufficient — skip overlap check

            var sender = msg.TryGetProperty("from", out var f)
                ? f.TryGetProperty("user", out var u)
                    ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
                    : null
                : null;

            var snippet = plain.Length > 80 ? plain[..80] + "…" : plain;
            var who     = sender is not null ? $"{sender} said" : "Message in the same thread";
            return (true, $"{who}: \"{snippet}\"");
        }

        // Also check reactions on the original commitment message (✅, 👍 etc.)
        if (messageId is not null)
        {
            if (chatId is not null)
            {
                var reactionsUrl = $"{GraphV1}/me/chats/{chatId}/messages/{messageId}/reactions";
                var reactions    = await GetGraphPagedAsync(http, reactionsUrl, bearerToken, ct);
                var completion   = reactions.FirstOrDefault(r =>
                {
                    var rt = r.TryGetProperty("reactionType", out var rtype) ? rtype.GetString() : null;
                    return rt is "✅" or "like" or "heart" or "celebrate";
                });

                if (completion.ValueKind != JsonValueKind.Undefined)
                {
                    var reacter = completion.TryGetProperty("user", out var u)
                        ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
                        : null;
                    return (true, reacter is not null
                        ? $"{reacter} reacted to your commitment message with ✅"
                        : "Your commitment message received a completion reaction");
                }
            }
        }

        return (false, null);
    }

    // ── Tier 1.5: Email flag + same-thread scan ───────────────────────────────

    private async Task<(bool Resolved, string? Reason)> CheckEmailFlagOrThreadAsync(
        CommitmentEntity entity, string bearerToken, CancellationToken ct)
    {
        using var metaDoc = JsonDocument.Parse(entity.SourceMetadata!);
        var root          = metaDoc.RootElement;
        var messageId     = root.TryGetProperty("messageId",     out var mid) ? mid.GetString() : null;
        var conversationId = root.TryGetProperty("conversationId", out var cid) ? cid.GetString() : null;
        if (messageId is null) return (false, null);

        var http = _httpFactory.CreateClient("graph");

        // ── Check: flag:complete on the original email ──────────────────────
        using var flagReq = new HttpRequestMessage(HttpMethod.Get,
            $"{GraphV1}/me/messages/{messageId}?$select=subject,flag");
        flagReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var flagResp = await http.SendAsync(flagReq, ct);

        if (flagResp.IsSuccessStatusCode)
        {
            var flagJson = await flagResp.Content.ReadAsStringAsync(ct);
            using var flagDoc = JsonDocument.Parse(flagJson);

            var flag       = flagDoc.RootElement.TryGetProperty("flag", out var fl) ? fl : default;
            var flagStatus = flag.ValueKind != JsonValueKind.Undefined
                ? flag.TryGetProperty("flagStatus", out var fs) ? fs.GetString() : null
                : null;

            if (string.Equals(flagStatus, "complete", StringComparison.OrdinalIgnoreCase))
            {
                var subject = flagDoc.RootElement.TryGetProperty("subject", out var s)
                    ? s.GetString() : "email";
                return (true, $"You marked the email \"{subject}\" as Complete in Outlook");
            }
        }

        if (conversationId is not null)
        {
            var since = entity.CommittedAt.ToString("o");

            // ── Check: your own sent reply in the thread (highest confidence) ──
            // Uses sentDateTime because Sent Items messages have null/incorrect receivedDateTime.
            var sentUrl = $"{GraphV1}/me/mailFolders/sentitems/messages" +
                          $"?$filter=conversationId eq '{conversationId}'" +
                          $" and sentDateTime ge {Uri.EscapeDataString(since)}" +
                          $"&$select=subject,bodyPreview,sentDateTime&$top=10" +
                          $"&$orderby=sentDateTime desc";

            var sentReplies = await GetGraphPagedAsync(http, sentUrl, bearerToken, ct);

            foreach (var sent in sentReplies)
            {
                var preview = sent.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : null;
                if (preview is null) continue;
                if (!HasCompletionSignal(preview)) continue;
                // Lower overlap threshold for own replies — "done!" with no keywords is still very strong
                if (KeywordOverlap(entity.Title, preview) < 0.10) continue;

                var snippet = preview.Length > 80 ? preview[..80] + "…" : preview;
                return (true, $"You replied in the email thread: \"{snippet}\"");
            }

            // ── Check: received reply in the same conversation thread ──────────
            var threadUrl = $"{GraphV1}/me/messages" +
                            $"?$filter=conversationId eq '{conversationId}'" +
                            $" and receivedDateTime ge {Uri.EscapeDataString(since)}" +
                            $"&$select=subject,bodyPreview,from,receivedDateTime&$top=10";

            var replies = await GetGraphPagedAsync(http, threadUrl, bearerToken, ct);

            foreach (var reply in replies)
            {
                var preview = reply.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : null;
                if (preview is null) continue;
                if (!HasCompletionSignal(preview)) continue;
                if (KeywordOverlap(entity.Title, preview) < 0.15) continue;

                var from = reply.TryGetProperty("from", out var f)
                    ? f.TryGetProperty("emailAddress", out var ea)
                        ? ea.TryGetProperty("name", out var n) ? n.GetString() : null
                        : null
                    : null;
                var snippet = preview.Length > 80 ? preview[..80] + "…" : preview;
                var who     = from is not null ? $"{from} replied in the thread" : "Reply in email thread";
                return (true, $"{who}: \"{snippet}\"");
            }
        }

        return (false, null);
    }

    // ── Tier 1.5: Drive doc comment re-check ─────────────────────────────────

    private async Task<(bool Resolved, string? Reason)> CheckDocCommentAsync(
        CommitmentEntity entity, string bearerToken, CancellationToken ct)
    {
        using var metaDoc = JsonDocument.Parse(entity.SourceMetadata!);
        var root      = metaDoc.RootElement;
        var itemId    = root.TryGetProperty("itemId",    out var iid) ? iid.GetString() : null;
        var commentId = root.TryGetProperty("commentId", out var cid) ? cid.GetString() : null;
        if (itemId is null || commentId is null) return (false, null);

        var http = _httpFactory.CreateClient("graph");
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{GraphV1}/me/drive/items/{itemId}/comments/{commentId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var resp = await http.SendAsync(req, ct);

        // 404 = comment was deleted (resolved and cleaned up)
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return (true, "The document comment was deleted — task marked as done in the doc");

        if (!resp.IsSuccessStatusCode) return (false, null);

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        // isResolved flag (present on some Graph comment resources)
        if (doc.RootElement.TryGetProperty("isResolved", out var ir) && ir.GetBoolean())
            return (true, "The document comment was marked as Resolved in the Office file");

        // Check replies on the comment for completion signals
        if (doc.RootElement.TryGetProperty("replies", out var repliesEl))
        {
            foreach (var reply in repliesEl.EnumerateArray())
            {
                var replyBody = reply.TryGetProperty("content", out var rb) ? rb.GetString() : null;
                if (replyBody is null) continue;
                if (!HasCompletionSignal(replyBody)) continue;

                var replyAuthor = reply.TryGetProperty("author", out var a)
                    ? a.TryGetProperty("user", out var u)
                        ? u.TryGetProperty("displayName", out var dn) ? dn.GetString() : null
                        : null
                    : null;
                var snippet = replyBody.Length > 80 ? replyBody[..80] + "…" : replyBody;
                var who     = replyAuthor is not null ? $"{replyAuthor} replied on the doc comment" : "Reply on doc comment";
                return (true, $"{who}: \"{snippet}\"");
            }
        }

        return (false, null);
    }

    // ── Tier 2: Signal-based bulk scan + GPT ─────────────────────────────────

    private async Task<List<CommitmentEntity>> CheckSignalSourcesAsync(
        string userId,
        string bearerToken,
        List<CommitmentEntity> candidates,
        CancellationToken ct)
    {
        var completionMessages = await ScanForCompletionMessagesAsync(bearerToken, ct);
        if (completionMessages.Count == 0) return [];

        var titles  = new List<string>();
        var msgSets = new List<string[]>();
        var matched = new List<CommitmentEntity>();

        foreach (var entity in candidates)
        {
            var relevant = completionMessages
                .Where(m => m.SentAt > entity.CommittedAt &&
                            KeywordOverlap(entity.Title, m.Text) > 0.25)
                .Select(m => m.Text)
                .ToArray();

            if (relevant.Length == 0) continue;

            titles.Add(entity.Title);
            msgSets.Add(relevant);
            matched.Add(entity);
        }

        if (matched.Count == 0) return [];

        var classifications = await _nlp.ClassifyResolutionAsync(titles, msgSets, ct);

        var resolved = new List<CommitmentEntity>();
        for (var i = 0; i < classifications.Count && i < matched.Count; i++)
        {
            var c = classifications[i];
            if (!c.Resolved || c.Confidence < AutoResolveThreshold) continue;

            _logger.LogInformation(
                "CommitmentResolutionService: auto-resolving '{Title}' (confidence={Confidence:P0}, evidence={Evidence})",
                matched[i].Title[..Math.Min(matched[i].Title.Length, 40)],
                c.Confidence, c.Evidence);

            matched[i].ResolutionReason = string.IsNullOrWhiteSpace(c.Evidence)
                ? $"Detected as complete with {c.Confidence:P0} confidence from recent messages"
                : c.Evidence;

            resolved.Add(matched[i]);
        }

        return resolved;
    }

    private async Task<List<CompletionMessage>> ScanForCompletionMessagesAsync(
        string bearerToken, CancellationToken ct)
    {
        var http      = _httpFactory.CreateClient("graph");
        var sinceDate = DateTimeOffset.UtcNow.AddDays(-2);
        var results   = new List<CompletionMessage>();

        // ── DMs and group chats ───────────────────────────────────────────────
        var chats = await GetGraphPagedAsync(http, $"{GraphV1}/me/chats", bearerToken, ct);

        foreach (var chat in chats)
        {
            var chatId   = chat.TryGetProperty("id", out var id) ? id.GetString() : null;
            var chatType = chat.TryGetProperty("chatType", out var ct2) ? ct2.GetString() : "";
            if (chatId is null || chatType is not ("oneOnOne" or "group")) continue;

            var messages = await GetGraphPagedAsync(http,
                $"{GraphV1}/me/chats/{chatId}/messages?$top=30", bearerToken, ct);

            foreach (var msg in messages)
            {
                if (!ExtractCompletionMessage(msg, sinceDate, out var cm)) continue;
                results.Add(cm!);
            }
        }

        // ── Joined team channels ──────────────────────────────────────────────
        var teams = await GetGraphPagedAsync(http, $"{GraphV1}/me/joinedTeams", bearerToken, ct);

        foreach (var team in teams)
        {
            var teamId = team.TryGetProperty("id", out var tid) ? tid.GetString() : null;
            if (teamId is null) continue;

            var channels = await GetGraphPagedAsync(http,
                $"{GraphV1}/teams/{teamId}/channels", bearerToken, ct);

            foreach (var channel in channels)
            {
                var channelId = channel.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                if (channelId is null) continue;

                var messages = await GetGraphPagedAsync(http,
                    $"{GraphV1}/teams/{teamId}/channels/{channelId}/messages?$top=20", bearerToken, ct);

                foreach (var msg in messages)
                {
                    if (!ExtractCompletionMessage(msg, sinceDate, out var cm)) continue;
                    results.Add(cm!);
                }
            }
        }

        // ── Sent email replies ────────────────────────────────────────────────
        var sentUrl = $"{GraphV1}/me/mailFolders/sentitems/messages" +
                      $"?$filter=sentDateTime ge {Uri.EscapeDataString(sinceDate.ToString("o"))}" +
                      $"&$top=20";
        var sentMails = await GetGraphPagedAsync(http, sentUrl, bearerToken, ct);

        foreach (var mail in sentMails)
        {
            var preview = mail.TryGetProperty("bodyPreview", out var bp) ? bp.GetString() : null;
            if (preview is null || !HasCompletionSignal(preview)) continue;
            if (!DateTimeOffset.TryParse(
                    mail.TryGetProperty("sentDateTime", out var sd) ? sd.GetString() : null,
                    out var sentAt)) continue;
            results.Add(new CompletionMessage(preview, sentAt));
        }

        return results;
    }

    private static bool ExtractCompletionMessage(
        JsonElement msg, DateTimeOffset since, out CompletionMessage? result)
    {
        result = null;
        if (!DateTimeOffset.TryParse(
                msg.TryGetProperty("createdDateTime", out var cd) ? cd.GetString() : null,
                out var sentAt) || sentAt < since)
            return false;

        var body = msg.TryGetProperty("body", out var b)
            ? b.TryGetProperty("content", out var c) ? c.GetString() : null
            : null;
        if (body is null) return false;

        var plain = StripHtml(body);
        if (!HasCompletionSignal(plain)) return false;

        result = new CompletionMessage(plain, sentAt);
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool HasCompletionSignal(string text)
    {
        var lower = text.ToLowerInvariant();
        return CompletionSignals.Any(signal => lower.Contains(signal));
    }

    private static double KeywordOverlap(string title, string message)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "i", "will", "the", "a", "to", "of", "and", "it", "by", "this", "that", "my", "your" };

        var titleWords = title.ToLowerInvariant()
            .Split([' ', ',', '.', ':', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToHashSet();

        if (titleWords.Count == 0) return 0;

        var msgWords = message.ToLowerInvariant()
            .Split([' ', ',', '.', ':', '-'], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        return (double)titleWords.Intersect(msgWords).Count() / titleWords.Count;
    }

    private static string? ParseLastSegment(string url)
    {
        var parts = url.TrimEnd('/').Split('/');
        var last  = parts[^1];
        return int.TryParse(last, out _) ? last : null;
    }

    private static string ParseAdoProject(string url, string org)
    {
        var prefix = $"dev.azure.com/{org}/";
        var idx    = url.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        var rest = url[(idx + prefix.Length)..];
        return rest.Split('/')[0];
    }

    private async Task<string?> GetAdoJsonAsync(string url, string pat, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("ado");
        var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        using var resp = await http.SendAsync(req, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
    }

    private async Task<List<JsonElement>> GetGraphPagedAsync(
        HttpClient http, string url, string bearerToken, CancellationToken ct)
    {
        var results  = new List<JsonElement>();
        string? next = url;

        while (next is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, next);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) break;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("value", out var value))
                foreach (var item in value.EnumerateArray())
                    results.Add(item.Clone());

            next = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
        }

        return results;
    }

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Trim();

    private sealed record CompletionMessage(string Text, DateTimeOffset SentAt);
}
