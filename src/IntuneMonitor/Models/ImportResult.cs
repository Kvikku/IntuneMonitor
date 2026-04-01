using System.Net;

namespace IntuneMonitor.Models;

/// <summary>
/// Result of a single policy import operation, providing structured error information
/// for per-policy error reporting.
/// </summary>
public record ImportResult
{
    /// <summary>Whether the import succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Name of the imported policy.</summary>
    public string? PolicyName { get; init; }

    /// <summary>HTTP status code returned by Graph (null when the request was not sent).</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>Error message when the import failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Raw error body returned by Graph API.</summary>
    public string? ErrorBody { get; init; }

    /// <summary>Error category for downstream reporting.</summary>
    public ImportErrorCategory ErrorCategory { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static ImportResult Succeeded(string? policyName) =>
        new() { Success = true, PolicyName = policyName };

    /// <summary>Creates a failed result from an HTTP response.</summary>
    public static ImportResult Failed(string? policyName, HttpStatusCode statusCode, string? errorBody) =>
        new()
        {
            Success = false,
            PolicyName = policyName,
            StatusCode = statusCode,
            ErrorBody = errorBody,
            ErrorCategory = CategorizeError(statusCode),
            ErrorMessage = FormatErrorMessage(policyName, statusCode, errorBody)
        };

    /// <summary>Creates a failed result from an exception.</summary>
    public static ImportResult FailedWithException(string? policyName, Exception ex) =>
        new()
        {
            Success = false,
            PolicyName = policyName,
            ErrorCategory = ImportErrorCategory.Unknown,
            ErrorMessage = $"Failed to import '{policyName}': {ex.Message}"
        };

    private static ImportErrorCategory CategorizeError(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => ImportErrorCategory.ValidationError,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ImportErrorCategory.AuthenticationError,
            HttpStatusCode.NotFound => ImportErrorCategory.NotFound,
            HttpStatusCode.Conflict => ImportErrorCategory.Conflict,
            HttpStatusCode.TooManyRequests => ImportErrorCategory.Throttled,
            >= HttpStatusCode.InternalServerError => ImportErrorCategory.ServerError,
            _ => ImportErrorCategory.Unknown
        };

    private static string FormatErrorMessage(string? policyName, HttpStatusCode statusCode, string? errorBody) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest =>
                $"Validation error importing '{policyName}' (400 Bad Request): the policy payload is invalid or missing required fields. {TruncateBody(errorBody)}",
            HttpStatusCode.Conflict =>
                $"Conflict importing '{policyName}' (409 Conflict): a policy with the same name or identifier already exists. {TruncateBody(errorBody)}",
            HttpStatusCode.NotFound =>
                $"Endpoint not found importing '{policyName}' (404 Not Found): the Graph API endpoint may not exist or the content type is unsupported. {TruncateBody(errorBody)}",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"Authorization failed importing '{policyName}' ({(int)statusCode}): insufficient permissions or expired credentials. {TruncateBody(errorBody)}",
            HttpStatusCode.TooManyRequests =>
                $"Throttled importing '{policyName}' (429 Too Many Requests): rate limit exceeded. {TruncateBody(errorBody)}",
            _ =>
                $"Failed to import '{policyName}' ({(int)statusCode} {statusCode}): {TruncateBody(errorBody)}"
        };

    /// <summary>Maximum number of characters to include from the Graph API error body.</summary>
    private const int MaxErrorBodyLength = 500;

    private static string TruncateBody(string? body) =>
        string.IsNullOrWhiteSpace(body) ? string.Empty :
        body.Length > MaxErrorBodyLength ? body[..MaxErrorBodyLength] + "..." : body;
}

/// <summary>
/// Categorizes import errors for structured reporting and downstream handling.
/// </summary>
public enum ImportErrorCategory
{
    /// <summary>No error.</summary>
    None,

    /// <summary>400 Bad Request — payload validation failed.</summary>
    ValidationError,

    /// <summary>401/403 — authentication or authorization failure.</summary>
    AuthenticationError,

    /// <summary>404 Not Found — endpoint or resource does not exist.</summary>
    NotFound,

    /// <summary>409 Conflict — resource already exists.</summary>
    Conflict,

    /// <summary>429 Too Many Requests — rate limiting.</summary>
    Throttled,

    /// <summary>5xx — server-side error.</summary>
    ServerError,

    /// <summary>Unclassified error.</summary>
    Unknown
}
