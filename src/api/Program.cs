using System.Text;
using System.Text.Json;
using Azure.Data.AppConfiguration;
using Azure.Data.Tables;
using CommitApi.Agents;
using CommitApi.Auth;
using CommitApi.Capacity;
using CommitApi.Config;
using CommitApi.Entities;
using CommitApi.Exceptions;
using CommitApi.Extractors;
using CommitApi.Graph;
using CommitApi.Models;
using CommitApi.Models.Agents;
using CommitApi.Models.Extraction;
using CommitApi.Replan;
using CommitApi.Repositories;
using CommitApi.Services;
using CommitApi.Webhooks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ─────────────────────────────────────────────────────────────
var tenantId        = builder.Configuration["TENANT_ID"]                      ?? Environment.GetEnvironmentVariable("TENANT_ID")                      ?? "";
var clientId        = builder.Configuration["CLIENT_ID"]                      ?? Environment.GetEnvironmentVariable("CLIENT_ID")                      ?? "";
var clientSecret    = builder.Configuration["CLIENT_SECRET"]                  ?? Environment.GetEnvironmentVariable("CLIENT_SECRET")                  ?? "";
var storageConn     = builder.Configuration["AZURE_STORAGE_CONN"]             ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN")             ?? "UseDevelopmentStorage=true";
var appConfigConn   = builder.Configuration["AZURE_APP_CONFIG_CONN"]          ?? Environment.GetEnvironmentVariable("AZURE_APP_CONFIG_CONN");
var environment     = builder.Configuration["COMMIT_ENV"]                     ?? Environment.GetEnvironmentVariable("COMMIT_ENV")                     ?? "dev";
var appInsightsConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
var webhookUrl      = builder.Configuration["WEBHOOK_NOTIFICATION_URL"]       ?? Environment.GetEnvironmentVariable("WEBHOOK_NOTIFICATION_URL")       ?? "https://localhost:5001/api/v1/webhook";
var clientState     = builder.Configuration["WEBHOOK_CLIENT_STATE"]           ?? Environment.GetEnvironmentVariable("WEBHOOK_CLIENT_STATE")           ?? Guid.NewGuid().ToString();

// ─── Azure Table Storage ───────────────────────────────────────────────────────
var tableClient = new TableClient(storageConn, "commitments");
await tableClient.CreateIfNotExistsAsync();
builder.Services.AddSingleton(tableClient);
builder.Services.AddSingleton<ICommitmentRepository, CommitmentRepository>();

// ─── Auth (MSAL OBO + Graph) ───────────────────────────────────────────────────
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd");
builder.Services.AddSingleton<IGraphClientFactory>(_ =>
    new GraphClientFactory(tenantId, clientId, clientSecret,
        _.GetRequiredService<ILogger<GraphClientFactory>>()));

// ─── Webhooks ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ISubscriptionManager>(sp =>
    new SubscriptionManager(
        sp.GetRequiredService<IGraphClientFactory>(),
        sp.GetRequiredService<ILogger<SubscriptionManager>>(),
        webhookUrl,
        clientState));
builder.Services.AddSingleton(sp =>
    new WebhookHandler(
        sp.GetRequiredService<ILogger<WebhookHandler>>(),
        sp.GetRequiredService<IAppInsightsClient>(),
        clientState));

// ─── HttpClient factory (Graph + ADO) ─────────────────────────────────────────
builder.Services.AddHttpClient("graph");
builder.Services.AddHttpClient("ado");

// ─── Signal Extractors (T-010, T-012, T-013, T-014) ───────────────────────────
builder.Services.AddSingleton<ITranscriptExtractor, TranscriptExtractor>();
builder.Services.AddSingleton<IChatExtractor, ChatExtractor>();
builder.Services.AddSingleton<IEmailExtractor, EmailExtractor>();
builder.Services.AddSingleton<IAdoExtractor, AdoExtractor>();

// ─── NLP + Pipeline Services (T-011, T-015, T-016) ───────────────────────────
builder.Services.AddSingleton<INlpPipeline, NlpPipeline>();
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();
builder.Services.AddSingleton<IEisenhowerScorer, EisenhowerScorer>();

// ─── Dependency Graph + Cascade Engine (T-020, T-021, T-022, T-023, T-026) ──
builder.Services.AddSingleton<IDependencyLinker, DependencyLinker>();
builder.Services.AddSingleton<ICascadeSimulator, CascadeSimulator>();
builder.Services.AddSingleton<IImpactScorer, ImpactScorer>();
builder.Services.AddSingleton<IVivaInsightsClient, VivaInsightsClient>();
builder.Services.AddSingleton<IReplanGenerator, ReplanGenerator>();

// ─── Risk Detector (T-024) — 15-min background polling ───────────────────────
builder.Services.AddHostedService<RiskDetector>();

// ─── Execution Agents (T-028, T-029, T-030, T-031, T-032) ────────────────────
builder.Services.AddSingleton<IStatusUpdateDrafter, StatusUpdateDrafter>();
builder.Services.AddSingleton<IOvercommitFirewall,  OvercommitFirewall>();
builder.Services.AddSingleton<ICalendarBlocker,     CalendarBlocker>();
builder.Services.AddSingleton<IPrReviewDrafter,     PrReviewDrafter>();

// ─── Motivation Service (T-034) ────────────────────────────────────────────────
builder.Services.AddSingleton<IMotivationService, MotivationService>();

// ─── Feature Flags ─────────────────────────────────────────────────────────────
ConfigurationClient? appConfigClient = appConfigConn is not null
    ? new ConfigurationClient(appConfigConn)
    : null;
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFeatureFlagService>(sp => new FeatureFlagService(
    appConfigClient,
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<ILogger<FeatureFlagService>>(),
    environment));

// ─── Application Insights ──────────────────────────────────────────────────────
builder.Services.AddApplicationInsightsTelemetry(opts =>
{
    opts.ConnectionString = appInsightsConn;
    opts.EnableAdaptiveSampling = false;
});
builder.Services.AddSingleton<IAppInsightsClient, AppInsightsClient>();

// ─── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new() { Title = "Commit API", Version = "v1" });
});

// ─── CORS (Teams tab needs this) ──────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ─── Middleware ────────────────────────────────────────────────────────────────
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Global exception → typed HTTP response
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (CommitException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            success  = false,
            error    = new { code = ex.Code, message = ex.Message },
            requestId = ctx.TraceIdentifier
        });
    }
});

// ─── Routes — /api/v1/ ─────────────────────────────────────────────────────────
var api = app.MapGroup("/api/v1");

// GET /api/v1/health — returns user info + connectivity status (T-005 acceptance criterion)
api.MapGet("/health", async (HttpContext http, IGraphClientFactory graphFactory) =>
{
    var token = ExtractBearerToken(http);
    string userName     = "unknown";
    bool graphConnected = false;

    if (token is not null)
    {
        try
        {
            var (displayName, _) = await graphFactory.GetCurrentUserAsync(token);
            userName        = displayName;
            graphConnected  = true;
        }
        catch (Exception)
        {
            // Degraded but still respond — auth issues logged in GraphClientFactory
        }
    }

    return Results.Ok(new
    {
        status          = graphConnected ? "ok" : "degraded",
        user            = userName,
        graphConnected,
        storageConnected = true, // TableClient.CreateIfNotExistsAsync succeeded at startup
        timestamp       = DateTimeOffset.UtcNow
    });
})
.WithName("Health")
.WithOpenApi();

// GET /api/v1/commitments/{userId}
api.MapGet("/commitments/{userId}", async (string userId, ICommitmentRepository repo) =>
{
    var entities = await repo.ListByOwnerAsync(userId);
    var dtos     = entities.Select(CommitmentResponse.From).ToList();
    return Results.Ok(new { success = true, data = dtos, requestId = Guid.NewGuid() });
})
.WithName("ListCommitments")
.WithOpenApi();

// DELETE /api/v1/users/{userId}/data — right-to-erasure (P-05, T-C06)
api.MapDelete("/users/{userId}/data", async (string userId, ICommitmentRepository repo,
    ISubscriptionManager subs, IAppInsightsClient insights, HttpContext http) =>
{
    var token = ExtractBearerToken(http);
    await repo.DeleteAllForUserAsync(userId);
    if (token is not null)
    {
        try { await subs.DeleteAllSubscriptionsAsync(token); }
        catch (Exception ex) { app.Logger.LogWarning(ex, "Subscription cleanup failed for erasure"); }
    }
    insights.TrackBusinessKpi("right-to-erasure", PiiScrubber.HashValue(userId), 1);
    return Results.NoContent();
})
.WithName("DeleteUserData")
.WithOpenApi();

// POST /api/v1/webhook — Graph change notification callback (T-008)
api.MapPost("/webhook", async (HttpRequest req, WebhookHandler handler) =>
{
    // Graph sends a validation query on subscription creation — respond immediately
    if (req.Query.TryGetValue("validationToken", out var token))
    {
        return Results.Content(token.ToString(), "text/plain");
    }

    // Read body
    using var reader = new StreamReader(req.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();

    // Validate HMAC signature
    var sig = req.Headers["X-Microsoft-Gryffindor-Signature"].ToString();
    var rawBytes = Encoding.UTF8.GetBytes(body);

    if (!handler.ValidateSignature(rawBytes, sig))
    {
        return Results.Unauthorized();
    }

    await handler.HandleAsync(body);
    return Results.Accepted();
})
.WithName("WebhookCallback")
.WithOpenApi();

// POST /api/v1/subscriptions — register Graph change notification subscriptions
api.MapPost("/subscriptions", async (HttpContext http, ISubscriptionManager subs) =>
{
    var token = ExtractBearerToken(http)
        ?? throw new AuthException("Authorization header required");

    var ids = await subs.EnsureSubscriptionsAsync(token);
    return Results.Ok(new { success = true, subscriptionIds = ids });
})
.WithName("EnsureSubscriptions")
.WithOpenApi();

// POST /api/v1/extract — trigger signal extraction for the caller (T-010–T-016)
api.MapPost("/extract", async (
    HttpContext http,
    ITranscriptExtractor transcripts,
    IChatExtractor chats,
    IEmailExtractor emails,
    IAdoExtractor ado,
    INlpPipeline nlp,
    IDeduplicationService dedup,
    IEisenhowerScorer scorer,
    ICommitmentRepository repo,
    IAppInsightsClient insights) =>
{
    var token  = ExtractBearerToken(http)
        ?? throw new AuthException("Authorization header required");
    var userId = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(userId))
        throw new ValidationException("userId query parameter is required");

    // ── Run all four extractors concurrently ──────────────────────────────────
    var t1 = transcripts.GetChunksAsync(token);
    var t2 = chats.ExtractAsync(token);
    var t3 = emails.ExtractAsync(token);
    var t4 = ado.ExtractAsync(userId, token);
    await Task.WhenAll(t1, t2, t3, t4);
    var transcriptChunks = t1.Result;
    var chatRaw          = t2.Result;
    var emailRaw         = t3.Result;
    var adoRaw           = t4.Result;

    // ── NLP pipeline on transcript chunks ────────────────────────────────────
    var transcriptRaw = await nlp.ExtractFromChunksAsync(transcriptChunks);

    // ── Merge all sources ─────────────────────────────────────────────────────
    var allRaw = transcriptRaw
        .Concat(chatRaw)
        .Concat(emailRaw)
        .Concat(adoRaw)
        .ToList();

    // ── Deduplicate ────────────────────────────────────────────────────────────
    var deduped = dedup.Deduplicate(allRaw);

    // ── Persist with Eisenhower priority ─────────────────────────────────────
    var upserted = 0;
    foreach (var raw in deduped)
    {
        var priority = scorer.Score(raw);
        var entity = new CommitmentEntity
        {
            PartitionKey      = userId,
            RowKey            = Guid.NewGuid().ToString(),
            Title             = raw.Title,
            Owner             = userId,
            WatchersJson      = JsonSerializer.Serialize(raw.WatcherUserIds),
            SourceType        = raw.SourceType.ToString(),
            SourceUrl         = raw.SourceUrl,
            SourceTimestamp   = raw.ExtractedAt,
            CommittedAt       = raw.ExtractedAt,
            DueAt             = raw.DueAt,
            Priority          = priority,
            Status            = "pending",
            ImpactScore       = 0,  // scored by cascade engine (Day 3)
        };

        await repo.UpsertAsync(entity);
        upserted++;
    }

    insights.TrackUserAction("extract", PiiScrubber.HashValue(userId), "commitments", new Dictionary<string, string>
    {
        ["raw"]     = allRaw.Count.ToString(),
        ["deduped"] = deduped.Count.ToString(),
        ["sources"] = "transcript,chat,email,ado"
    });

    // ── Register user for risk detection ─────────────────────────────────────
    RiskDetector.RegisteredUsers[userId] = DateTimeOffset.UtcNow;

    return Results.Ok(new
    {
        success    = true,
        extracted  = allRaw.Count,
        deduped    = deduped.Count,
        upserted,
        requestId  = http.TraceIdentifier
    });
})
.WithName("ExtractCommitments")
.WithOpenApi();

// POST /api/v1/graph/build?userId=X — build dependency graph (T-020)
api.MapPost("/graph/build", async (
    HttpContext http,
    IDependencyLinker linker,
    IAppInsightsClient insights) =>
{
    var token  = ExtractBearerToken(http)
        ?? throw new AuthException("Authorization header required");
    var userId = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(userId))
        throw new ValidationException("userId query parameter is required");

    RiskDetector.RegisteredUsers[userId] = DateTimeOffset.UtcNow;
    var edges = await linker.BuildGraphAsync(userId, token);

    insights.TrackUserAction("graph-build", PiiScrubber.HashValue(userId), "graph",
        new Dictionary<string, string> { ["edges"] = edges.Count.ToString() });

    return Results.Ok(new { success = true, edgeCount = edges.Count, requestId = Guid.NewGuid() });
})
.WithName("BuildDependencyGraph")
.WithOpenApi();

// POST /api/v1/graph/cascade?rootTaskId=X&userId=U&slipDays=N — cascade simulation (T-021, T-022)
api.MapPost("/graph/cascade", async (
    HttpContext http,
    ICascadeSimulator cascade,
    IImpactScorer scorer,
    ICommitmentRepository repo,
    IAppInsightsClient insights) =>
{
    var rootTaskId = http.Request.Query["rootTaskId"].ToString();
    var userId     = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(rootTaskId) || string.IsNullOrEmpty(userId))
        throw new ValidationException("rootTaskId and userId query parameters are required");

    if (!int.TryParse(http.Request.Query["slipDays"], out var slipDays) || slipDays < 0)
        slipDays = 1;

    var result    = await cascade.SimulateAsync(rootTaskId, userId, slipDays);
    var allIds    = result.AffectedTasks.Select(t => t.TaskId).ToHashSet();
    var allItems  = await repo.ListByOwnerAsync(userId);
    var affected  = allItems.Where(e => allIds.Contains(e.RowKey)).ToList();
    var impactScore = scorer.Score(result, affected);

    // Persist updated impact score onto the root task
    var root = affected.FirstOrDefault(e => e.RowKey == rootTaskId);
    if (root is not null)
    {
        root.ImpactScore = impactScore;
        await repo.UpsertAsync(root);
    }

    insights.TrackUserAction("cascade", PiiScrubber.HashValue(userId), "graph",
        new Dictionary<string, string>
        {
            ["affected"] = result.TotalTasksAffected.ToString(),
            ["score"]    = impactScore.ToString(),
            ["slipDays"] = slipDays.ToString()
        });

    return Results.Ok(new
    {
        success     = true,
        rootTaskId,
        slipDays,
        impactScore,
        affectedCount = result.TotalTasksAffected,
        affectedTasks = result.AffectedTasks,
        requestId   = Guid.NewGuid()
    });
})
.WithName("SimulateCascade")
.WithOpenApi();

// GET /api/v1/capacity?userId=X — Viva Insights capacity snapshot (T-023)
api.MapGet("/capacity", async (
    HttpContext http,
    IVivaInsightsClient viva,
    IAppInsightsClient insights) =>
{
    var token  = ExtractBearerToken(http)
        ?? throw new AuthException("Authorization header required");
    var userId = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(userId))
        throw new ValidationException("userId query parameter is required");

    RiskDetector.RegisteredUsers[userId] = DateTimeOffset.UtcNow;
    var snapshot = await viva.GetCapacityAsync(userId, token);

    insights.TrackUserAction("capacity", PiiScrubber.HashValue(userId), "capacity",
        new Dictionary<string, string>
        {
            ["loadIndex"]    = snapshot.LoadIndex.ToString("F2"),
            ["burnoutTrend"] = snapshot.BurnoutTrend.ToString("F2")
        });

    return Results.Ok(new
    {
        success      = true,
        loadIndex    = snapshot.LoadIndex,
        burnoutTrend = snapshot.BurnoutTrend,
        freeSlots    = snapshot.FreeSlots,
        requestId    = Guid.NewGuid()
    });
})
.WithName("GetCapacity")
.WithOpenApi();

// POST /api/v1/graph/replan?rootTaskId=X&userId=U — replan options (T-026)
api.MapPost("/graph/replan", async (
    HttpContext http,
    ICascadeSimulator cascade,
    IReplanGenerator replan,
    IAppInsightsClient insights) =>
{
    var rootTaskId = http.Request.Query["rootTaskId"].ToString();
    var userId     = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(rootTaskId) || string.IsNullOrEmpty(userId))
        throw new ValidationException("rootTaskId and userId query parameters are required");

    if (!int.TryParse(http.Request.Query["slipDays"], out var slipDays) || slipDays < 0)
        slipDays = 1;

    var cascadeResult = await cascade.SimulateAsync(rootTaskId, userId, slipDays);
    var options       = await replan.GenerateAsync(cascadeResult, userId);

    insights.TrackUserAction("replan", PiiScrubber.HashValue(userId), "replan",
        new Dictionary<string, string> { ["options"] = options.Count.ToString() });

    return Results.Ok(new { success = true, options, requestId = Guid.NewGuid() });
})
.WithName("GetReplanOptions")
.WithOpenApi();

// GET /api/v1/users/{userId}/motivation — motivation state (T-034 + psychology layer)
api.MapGet("/users/{userId}/motivation", async (
    string              userId,
    IMotivationService  motivation,
    IAppInsightsClient  insights) =>
{
    var state = await motivation.GetStateAsync(userId);

    insights.TrackUserAction("motivation-view", PiiScrubber.HashValue(userId), "psychology",
        new Dictionary<string, string>
        {
            ["score"]  = state.DeliveryScore.ToString(),
            ["streak"] = state.StreakDays.ToString(),
            ["level"]  = state.CompetencyLevel.ToString(),
        });

    return Results.Ok(new
    {
        success               = true,
        userId                = state.UserId,
        deliveryScore         = state.DeliveryScore,
        deliveryScorePrevious = state.DeliveryScorePrevious,
        streakDays            = state.StreakDays,
        totalXp               = state.TotalXp,
        competencyLevel       = state.CompetencyLevel,
        onTimeRate            = state.OnTimeRate,
        cascadeHealthRate     = state.CascadeHealthRate,
        triggersShownToday    = state.TriggersShownToday,
        lastStreakDate        = state.LastStreakDate,
        requestId             = Guid.NewGuid(),
    });
})
.WithName("GetMotivationState")
.WithOpenApi();

// POST /api/v1/approvals — handle Approve / Edit / Skip decisions (T-034)
api.MapPost("/approvals", async (
    HttpContext         http,
    ICommitmentRepository repo,
    ICalendarBlocker    calendarBlocker,
    IAppInsightsClient  insights) =>
{
    // Parse body
    ApprovalDecision? decision;
    try
    {
        decision = await http.Request.ReadFromJsonAsync<ApprovalDecision>();
    }
    catch
    {
        throw new ValidationException("Request body must be a valid ApprovalDecision JSON object");
    }

    if (decision is null || string.IsNullOrEmpty(decision.CommitmentId) || string.IsNullOrEmpty(decision.DraftId))
        throw new ValidationException("commitmentId and draftId are required");

    // ── Load commitment ────────────────────────────────────────────────────────
    // CommitmentId is the RowKey; we need the PartitionKey (userId) — inferred from caller
    var token  = ExtractBearerToken(http);
    var userId = http.Request.Query["userId"].ToString();
    if (string.IsNullOrEmpty(userId))
        throw new ValidationException("userId query parameter is required");

    var entity = await repo.GetAsync(userId, decision.CommitmentId);
    if (entity is null)
        throw new NotFoundException($"Commitment {decision.CommitmentId} not found");

    // ── Update draft status ────────────────────────────────────────────────────
    if (entity.AgentDraftJson is not null)
    {
        try
        {
            var draft = JsonSerializer.Deserialize<AgentDraft>(entity.AgentDraftJson);
            if (draft is not null && draft.DraftId == decision.DraftId)
            {
                var updatedDraft = draft with
                {
                    Status        = decision.Decision switch
                    {
                        "approve" => "approved",
                        "edit"    => "edited",
                        "skip"    => "skipped",
                        _         => draft.Status,
                    },
                    EditedContent = decision.EditedContent,
                };
                entity.AgentDraftJson = JsonSerializer.Serialize(updatedDraft);
                entity.LastActivity   = DateTimeOffset.UtcNow;
                await repo.UpsertAsync(entity);
            }
        }
        catch (JsonException ex)
        {
            app.Logger.LogWarning(ex, "Failed to deserialize AgentDraftJson for commitment {Id}", decision.CommitmentId);
        }
    }

    // ── Side effects per decision ──────────────────────────────────────────────
    var sideEffectResult = decision.Decision switch
    {
        "approve" => "executed",
        "edit"    => "edited-and-executed",
        "skip"    => "dismissed",
        _         => "unknown",
    };

    // Calendar block: if the draft is for a calendar event and was approved, create it
    if ((decision.Decision is "approve" or "edit") && token is not null)
    {
        // Fire-and-forget — calendar creation is best-effort
        _ = calendarBlocker.BlockFocusTimeAsync(userId, token, entity.Title)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    app.Logger.LogWarning(t.Exception, "Calendar block failed for commitment {Id}", decision.CommitmentId);
            }, TaskScheduler.Default);
    }

    insights.TrackUserAction("approval", PiiScrubber.HashValue(userId), "approvals",
        new Dictionary<string, string>
        {
            ["decision"]  = decision.Decision,
            ["draftId"]   = PiiScrubber.HashValue(decision.DraftId),
            ["result"]    = sideEffectResult,
        });

    return Results.Ok(new
    {
        success    = true,
        decision   = decision.Decision,
        result     = sideEffectResult,
        requestId  = http.TraceIdentifier,
    });
})
.WithName("HandleApproval")
.WithOpenApi();

app.Run();

// ─── Helpers ───────────────────────────────────────────────────────────────────
static string? ExtractBearerToken(HttpContext ctx)
{
    var auth = ctx.Request.Headers["Authorization"].ToString();
    return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? auth["Bearer ".Length..]
        : null;
}
