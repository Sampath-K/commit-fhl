using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Caching.Memory;

namespace CommitApi.Config;

/// <summary>
/// Contract for feature flag evaluation.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Returns true if the named feature flag is enabled.
    /// Checks the Azure App Configuration label matching the current environment (dev/pilot/ga).
    /// Falls back to environment variable override, then hard-coded defaults.
    /// </summary>
    /// <param name="flagName">Feature flag key (e.g. "commit.feature.psychologyLayer").</param>
    /// <param name="userId">Optional AAD Object ID — reserved for future per-user targeting.</param>
    Task<bool> IsEnabledAsync(string flagName, string? userId = null);
}

/// <summary>
/// Azure App Configuration backed feature flag service.
/// Caches flag values for 60 seconds to avoid per-request latency (P-02).
/// Falls back to environment variable FEATURE_{FLAG_KEY_UPPER} if App Config is unavailable.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly ConfigurationClient? _appConfigClient;
    private readonly IMemoryCache _cache;
    private readonly string _environment;
    private readonly ILogger<FeatureFlagService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>Default flag values — used when App Config is unreachable and no env var override.</summary>
    private static readonly IReadOnlyDictionary<string, bool> DefaultFlags =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["commit.feature.psychologyLayer"]  = true,
            ["commit.feature.deliveryScore"]    = true,
            ["commit.feature.streakTracking"]   = true,
            ["commit.feature.cascadeAlerts"]    = true,
            ["commit.feature.overcommitWarning"] = true,
        };

    public FeatureFlagService(
        ConfigurationClient? appConfigClient,
        IMemoryCache cache,
        ILogger<FeatureFlagService> logger,
        string environment = "dev")
    {
        _appConfigClient = appConfigClient;
        _cache = cache;
        _logger = logger;
        _environment = environment;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string flagName, string? userId = null)
    {
        var cacheKey = $"ff:{_environment}:{flagName}";

        if (_cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var value = await ResolveAsync(flagName);
        _cache.Set(cacheKey, value, CacheTtl);
        return value;
    }

    private async Task<bool> ResolveAsync(string flagName)
    {
        // 1. Try Azure App Configuration
        if (_appConfigClient is not null)
        {
            try
            {
                var setting = await _appConfigClient.GetConfigurationSettingAsync(
                    flagName, label: _environment);
                if (bool.TryParse(setting.Value.Value, out var parsed))
                {
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "App Configuration unavailable for flag {FlagName}; falling back to env/defaults", flagName);
            }
        }

        // 2. Environment variable override: FEATURE_COMMIT_FEATURE_PSYCHOLOGYLAYER
        var envKey = "FEATURE_" + flagName.Replace('.', '_').Replace('-', '_').ToUpperInvariant();
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (envValue is not null && bool.TryParse(envValue, out var envParsed))
        {
            return envParsed;
        }

        // 3. Hard-coded defaults (dev = all on, pilot/ga = all off by default)
        if (DefaultFlags.TryGetValue(flagName, out var defaultValue))
        {
            return _environment == "dev" && defaultValue;
        }

        return false;
    }
}
