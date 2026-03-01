using System.Text;
using Azure.Data.AppConfiguration;
using Azure.Data.Tables;
using CommitApi.Auth;
using CommitApi.Config;
using CommitApi.Exceptions;
using CommitApi.Repositories;
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
    var items = await repo.ListByOwnerAsync(userId);
    return Results.Ok(new { success = true, data = items, requestId = Guid.NewGuid() });
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

app.Run();

// ─── Helpers ───────────────────────────────────────────────────────────────────
static string? ExtractBearerToken(HttpContext ctx)
{
    var auth = ctx.Request.Headers["Authorization"].ToString();
    return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? auth["Bearer ".Length..]
        : null;
}
