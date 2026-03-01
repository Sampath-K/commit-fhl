using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommitApi.Config;

/// <summary>
/// Scrubs PII from telemetry payloads before they are emitted to Application Insights (P-12).
/// Rules:
///   - Remove fields: rawText, title (commitment text)
///   - Hash fields: userId, owner, watchers (SHA-256 + salt — pseudonymization, not anonymization)
///   - Truncate string fields longer than 200 characters
/// </summary>
public static class PiiScrubber
{
    private static readonly string Salt =
        Environment.GetEnvironmentVariable("PII_HASH_SALT") ?? "commit-fhl-default-salt";

    private static readonly HashSet<string> FieldsToRemove = new(StringComparer.OrdinalIgnoreCase)
    {
        "rawText", "title", "messageBody", "subject", "content"
    };

    private static readonly HashSet<string> FieldsToHash = new(StringComparer.OrdinalIgnoreCase)
    {
        "userId", "owner", "hashedUserId", "oid", "upn", "email"
    };

    /// <summary>
    /// Scrubs a dictionary of telemetry properties in-place.
    /// Removes PII fields, hashes identifiers, truncates long strings.
    /// </summary>
    /// <param name="properties">The telemetry properties dictionary to scrub.</param>
    public static void Scrub(IDictionary<string, string> properties)
    {
        var keysToRemove = properties.Keys
            .Where(k => FieldsToRemove.Contains(k))
            .ToList();

        foreach (var key in keysToRemove)
        {
            properties.Remove(key);
        }

        var keysToProcess = properties.Keys.ToList();
        foreach (var key in keysToProcess)
        {
            var value = properties[key];

            if (FieldsToHash.Contains(key))
            {
                properties[key] = HashValue(value);
            }
            else if (value.Length > 200)
            {
                properties[key] = value[..197] + "...";
            }
        }
    }

    /// <summary>
    /// Scrubs a JSON object by recursively applying PII rules.
    /// Returns a new scrubbed JsonObject — the original is not mutated.
    /// </summary>
    /// <param name="node">JSON node to scrub.</param>
    /// <returns>Scrubbed copy.</returns>
    public static JsonNode? ScrubJson(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var kvp in obj)
            {
                if (FieldsToRemove.Contains(kvp.Key)) continue;

                if (FieldsToHash.Contains(kvp.Key) && kvp.Value is JsonValue strVal &&
                    strVal.TryGetValue<string>(out var raw))
                {
                    result[kvp.Key] = HashValue(raw);
                }
                else
                {
                    result[kvp.Key] = ScrubJson(kvp.Value?.DeepClone());
                }
            }
            return result;
        }

        if (node is JsonArray arr)
        {
            var resultArr = new JsonArray();
            foreach (var item in arr)
            {
                resultArr.Add(ScrubJson(item?.DeepClone()));
            }
            return resultArr;
        }

        // Scalar: truncate long strings
        if (node is JsonValue val && val.TryGetValue<string>(out var str) && str.Length > 200)
        {
            return JsonValue.Create(str[..197] + "...");
        }

        return node.DeepClone();
    }

    /// <summary>
    /// Hashes a string value with SHA-256 + salt for pseudonymization.
    /// The salt is configurable via PII_HASH_SALT environment variable.
    /// </summary>
    public static string HashValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var bytes = Encoding.UTF8.GetBytes(Salt + value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16]; // 16-char prefix for readability
    }
}
