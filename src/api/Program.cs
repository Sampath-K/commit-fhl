using Azure.Data.AppConfiguration;
using Azure.Data.Tables;
using Azure.Identity;
using CommitApi.Config;
using CommitApi.Exceptions;
using CommitApi.Repositories;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ─────────────────────────────────────────────────────────────
var tenantId   = builder.Configuration["TENANT_ID"]   ?? Environment.GetEnvironmentVariable("TENANT_ID");
var clientId   = builder.Configuration["CLIENT_ID"]   ?? Environment.GetEnvironmentVariable("CLIENT_ID");
var storageConn = builder.Configuration["AZURE_STORAGE_CONN"] ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONN") ?? "UseDevelopmentStorage=true";
var appConfigConn = builder.Configuration["AZURE_APP_CONFIG_CONN"] ?? Environment.GetEnvironmentVariable("AZURE_APP_CONFIG_CONN");
var environment = builder.Configuration["COMMIT_ENV"] ?? Environment.GetEnvironmentVariable("COMMIT_ENV") ?? "dev";
var appInsightsConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

// ─── Azure Table Storage ───────────────────────────────────────────────────────
var tableClient = new TableClient(storageConn, "commitments");
await tableClient.CreateIfNotExistsAsync();
builder.Services.AddSingleton(tableClient);
builder.Services.AddSingleton<ICommitmentRepository, CommitmentRepository>();

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
    opts.EnableAdaptiveSampling = false; // Full fidelity for FHL demo
});
builder.Services.AddSingleton<IAppInsightsClient, AppInsightsClient>();

// ─── Auth ──────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication().AddMicrosoftIdentityWebApi(builder.Configuration);

// ─── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new() { Title = "Commit API", Version = "v1" });
});

var app = builder.Build();

// ─── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Global exception → typed HTTP response middleware
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
            success = false,
            error = new { code = ex.Code, message = ex.Message },
            requestId = ctx.TraceIdentifier
        });
    }
});

// ─── Routes — /api/v1/ ─────────────────────────────────────────────────────────
var api = app.MapGroup("/api/v1");

api.MapGet("/health", (ICommitmentRepository repo) =>
    Results.Ok(new
    {
        status = "ok",
        storageConnected = true,
        timestamp = DateTimeOffset.UtcNow
    }))
    .WithName("Health")
    .WithOpenApi();

// Commitment routes — stubs for now, wired up in T-007 implementation tasks
api.MapGet("/commitments/{userId}", async (string userId, ICommitmentRepository repo) =>
{
    var items = await repo.ListByOwnerAsync(userId);
    return Results.Ok(new { success = true, data = items, requestId = Guid.NewGuid() });
})
.WithName("ListCommitments")
.WithOpenApi();

api.MapDelete("/users/{userId}/data", async (string userId, ICommitmentRepository repo,
    IAppInsightsClient insights) =>
{
    await repo.DeleteAllForUserAsync(userId);
    insights.TrackBusinessKpi("right-to-erasure", PiiScrubber.HashValue(userId), 1);
    return Results.NoContent();
})
.WithName("DeleteUserData")
.WithOpenApi();

app.Run();
