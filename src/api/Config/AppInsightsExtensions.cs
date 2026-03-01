using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace CommitApi.Config;

/// <summary>
/// Application Insights telemetry client for the Commit API.
/// All events are PII-scrubbed before emission (P-12).
/// Supports 4 event types: userAction, error, performance, businessKpi.
/// </summary>
public interface IAppInsightsClient
{
    /// <summary>Tracks a user action event (approval, skip, edit, etc.).</summary>
    void TrackUserAction(string eventType, string hashedUserId, string featureArea,
        IDictionary<string, string>? properties = null);

    /// <summary>Tracks an error event — scrubs PII before emission.</summary>
    void TrackError(Exception exception, string operation,
        IDictionary<string, string>? properties = null);

    /// <summary>Tracks a performance measurement (latency, throughput).</summary>
    void TrackPerformance(string operationName, TimeSpan duration,
        IDictionary<string, string>? properties = null);

    /// <summary>Tracks a business KPI event (commitment extracted, cascade detected, etc.).</summary>
    void TrackBusinessKpi(string kpiType, string hashedUserId, int count,
        double? value = null, IDictionary<string, string>? properties = null);
}

/// <summary>
/// Application Insights implementation. Falls back to structured console logging
/// when the connection string is not configured (local dev without App Insights).
/// </summary>
public sealed class AppInsightsClient : IAppInsightsClient
{
    private readonly TelemetryClient? _client;
    private readonly ILogger<AppInsightsClient> _logger;

    public AppInsightsClient(TelemetryClient? client, ILogger<AppInsightsClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public void TrackUserAction(string eventType, string hashedUserId, string featureArea,
        IDictionary<string, string>? properties = null)
    {
        var props = BuildScrubbed(properties);
        props["eventType"] = eventType;
        props["hashedUserId"] = hashedUserId;  // already hashed by caller
        props["featureArea"] = featureArea;
        props["category"] = "userAction";

        EmitEvent("CommitUserAction", props);
    }

    /// <inheritdoc />
    public void TrackError(Exception exception, string operation,
        IDictionary<string, string>? properties = null)
    {
        var props = BuildScrubbed(properties);
        props["operation"] = operation;
        props["errorType"] = exception.GetType().Name;
        // Message may contain PII — hash it rather than log raw
        props["messageHash"] = PiiScrubber.HashValue(exception.Message);

        if (_client is not null)
        {
            var telemetry = new ExceptionTelemetry(exception);
            foreach (var kvp in props) telemetry.Properties[kvp.Key] = kvp.Value;
            _client.TrackException(telemetry);
        }
        else
        {
            _logger.LogError(exception, "[AppInsights] Error in {Operation} | Props: {@Props}", operation, props);
        }
    }

    /// <inheritdoc />
    public void TrackPerformance(string operationName, TimeSpan duration,
        IDictionary<string, string>? properties = null)
    {
        var props = BuildScrubbed(properties);
        props["operationName"] = operationName;
        props["durationMs"] = duration.TotalMilliseconds.ToString("F0");
        props["category"] = "performance";

        if (_client is not null)
        {
            var metric = new MetricTelemetry($"CommitPerf_{operationName}", duration.TotalMilliseconds);
            foreach (var kv in props) metric.Properties[kv.Key] = kv.Value;
            _client.TrackMetric(metric);
        }
        else
        {
            _logger.LogInformation("[AppInsights] Perf {Op}: {DurationMs}ms | Props: {@Props}",
                operationName, duration.TotalMilliseconds.ToString("F0"), props);
        }
    }

    /// <inheritdoc />
    public void TrackBusinessKpi(string kpiType, string hashedUserId, int count,
        double? value = null, IDictionary<string, string>? properties = null)
    {
        var props = BuildScrubbed(properties);
        props["kpiType"] = kpiType;
        props["hashedUserId"] = hashedUserId;
        props["count"] = count.ToString();
        props["category"] = "businessKpi";
        if (value.HasValue) props["value"] = value.Value.ToString("F2");

        EmitEvent("CommitBusinessKpi", props);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string> BuildScrubbed(IDictionary<string, string>? input)
    {
        var props = input is not null
            ? new Dictionary<string, string>(input, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        PiiScrubber.Scrub(props);
        return props;
    }

    private void EmitEvent(string eventName, Dictionary<string, string> props)
    {
        if (_client is not null)
        {
            _client.TrackEvent(eventName, props);
        }
        else
        {
            _logger.LogInformation("[AppInsights] Event {Name} | Props: {@Props}", eventName, props);
        }
    }
}
