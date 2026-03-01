namespace CommitApi.Exceptions;

/// <summary>
/// Base exception for all typed errors in the Commit API.
/// Always throw a subclass — never throw CommitException directly.
/// </summary>
public abstract class CommitException : Exception
{
    /// <summary>HTTP status code to return for this error type.</summary>
    public int StatusCode { get; }

    /// <summary>Machine-readable error code for API responses.</summary>
    public string Code { get; }

    /// <summary>Optional structured context for logging (never include PII — P-12).</summary>
    public IReadOnlyDictionary<string, object>? Context { get; }

    protected CommitException(string message, int statusCode, string code,
        IReadOnlyDictionary<string, object>? context = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Context = context;
    }
}

/// <summary>Input validation failed (400).</summary>
public sealed class ValidationException : CommitException
{
    public ValidationException(string message, string? fieldName = null)
        : base(message, 400, "VALIDATION_ERROR",
            fieldName is not null
                ? new Dictionary<string, object> { ["field"] = fieldName }
                : null)
    { }
}

/// <summary>Authentication or authorization failed (401/403).</summary>
public sealed class AuthException : CommitException
{
    public AuthException(string message)
        : base(message, 401, "AUTH_ERROR") { }
}

/// <summary>Microsoft Graph API call failed (502).</summary>
public sealed class GraphException : CommitException
{
    public GraphException(string message, string? graphErrorCode = null)
        : base(message, 502, "GRAPH_ERROR",
            graphErrorCode is not null
                ? new Dictionary<string, object> { ["graphErrorCode"] = graphErrorCode }
                : null)
    { }
}

/// <summary>Azure Table Storage call failed (503).</summary>
public sealed class StorageException : CommitException
{
    public StorageException(string message, string? tableName = null)
        : base(message, 503, "STORAGE_ERROR",
            tableName is not null
                ? new Dictionary<string, object> { ["tableName"] = tableName }
                : null)
    { }
}

/// <summary>Requested resource not found (404).</summary>
public sealed class NotFoundException : CommitException
{
    public NotFoundException(string message)
        : base(message, 404, "NOT_FOUND") { }
}

/// <summary>Azure OpenAI call failed (503).</summary>
public sealed class AiException : CommitException
{
    public AiException(string message, string? model = null)
        : base(message, 503, "AI_ERROR",
            model is not null
                ? new Dictionary<string, object> { ["model"] = model }
                : null)
    { }
}
