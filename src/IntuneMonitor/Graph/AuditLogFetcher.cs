using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Fetches Intune audit events from Microsoft Graph with throttling support.
/// Handles HTTP 429 (Too Many Requests) by respecting the Retry-After header.
/// </summary>
public class AuditLogFetcher
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };
    private readonly ILogger<AuditLogFetcher> _logger;

    /// <summary>Maximum number of events per page to request from Graph.</summary>
    private const int PageSize = 100;

    /// <summary>Default delay in seconds when no Retry-After header is present on a 429 response.</summary>
    private const int DefaultRetryDelaySeconds = 30;

    /// <summary>Maximum number of retries for transient/throttle failures per request.</summary>
    private const int MaxRetries = 5;

    /// <summary>Small delay between page requests to reduce throttling risk.</summary>
    private static readonly TimeSpan PageRequestDelay = TimeSpan.FromMilliseconds(500);

    public AuditLogFetcher(TokenCredential credential, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AuditLogFetcher>();
    }

    /// <summary>
    /// Fetches audit events for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to look back (1–30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit events.</returns>
    public async Task<List<AuditEvent>> FetchAuditEventsAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 30)
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be between 1 and 30.");

        var token = await GetAccessTokenAsync(cancellationToken);
        using var httpClient = CreateHttpClient(token);

        var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var url = $"https://graph.microsoft.com/beta/deviceManagement/auditEvents"
                + $"?$filter=activityDateTime ge {since}"
                + $"&$orderby=activityDateTime desc"
                + $"&$top={PageSize}";

        var events = new List<AuditEvent>();
        int pageCount = 0;

        while (url != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;

            _logger.LogDebug("Fetching audit log page {PageNumber}...", pageCount);

            var json = await SendWithRetryAsync(httpClient, url, cancellationToken);
            if (json == null)
                break;

            var root = JsonSerializer.Deserialize<JsonElement>(json);

            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    var auditEvent = ParseAuditEvent(item);
                    if (auditEvent != null)
                        events.Add(auditEvent);
                }

                _logger.LogDebug("Page {PageNumber}: {Count} event(s) fetched", pageCount, valueArray.GetArrayLength());
            }

            // OData paging
            url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                ? nextProp.GetString()
                : null;

            // Pace requests to reduce throttling risk
            if (url != null)
                await Task.Delay(PageRequestDelay, cancellationToken);
        }

        _logger.LogInformation("Fetched {TotalEvents} audit event(s) across {PageCount} page(s)", events.Count, pageCount);
        return events;
    }

    /// <summary>
    /// Sends a GET request with retry logic for HTTP 429 and transient 5xx errors.
    /// </summary>
    private async Task<string?> SendWithRetryAsync(
        HttpClient httpClient,
        string url,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{MaxRetries}), retrying...", attempt + 1, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(DefaultRetryDelaySeconds), cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = GetRetryAfterSeconds(response);
                _logger.LogWarning("Throttled (HTTP 429). Waiting {RetryAfterSeconds}s before retry (attempt {Attempt}/{MaxRetries})",
                    retryAfter, attempt + 1, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken);
                continue;
            }

            if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
            {
                var delay = (int)Math.Pow(2, attempt) * 5;
                _logger.LogWarning("Server error (HTTP {StatusCode}). Retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    (int)response.StatusCode, delay, attempt + 1, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                continue;
            }

            // Non-retryable error
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Audit log request failed with HTTP {StatusCode}: {ErrorBody}",
                (int)response.StatusCode, errorBody);
            return null;
        }

        _logger.LogError("Audit log request failed after {MaxRetries} retries", MaxRetries);
        return null;
    }

    /// <summary>
    /// Parses a JSON element into an <see cref="AuditEvent"/>.
    /// </summary>
    private static AuditEvent? ParseAuditEvent(JsonElement item)
    {
        try
        {
            return new AuditEvent
            {
                Id = GetString(item, "id"),
                DisplayName = GetString(item, "displayName"),
                ComponentName = GetString(item, "componentName"),
                Activity = GetString(item, "activity"),
                ActivityType = GetString(item, "activityType"),
                ActivityResult = GetString(item, "activityResult"),
                ActivityDateTime = TryParseDateTime(item, "activityDateTime") ?? DateTime.MinValue,
                Actor = ParseActor(item),
                Resources = ParseResources(item)
            };
        }
        catch
        {
            return null;
        }
    }

    private static AuditActor? ParseActor(JsonElement item)
    {
        if (!item.TryGetProperty("actor", out var actor) || actor.ValueKind != JsonValueKind.Object)
            return null;

        return new AuditActor
        {
            ApplicationDisplayName = GetString(actor, "applicationDisplayName"),
            UserPrincipalName = GetString(actor, "userPrincipalName"),
            IpAddress = GetString(actor, "ipAddress")
        };
    }

    private static List<AuditResource> ParseResources(JsonElement item)
    {
        var resources = new List<AuditResource>();
        if (!item.TryGetProperty("resources", out var resourcesArray)
            || resourcesArray.ValueKind != JsonValueKind.Array)
            return resources;

        foreach (var resource in resourcesArray.EnumerateArray())
        {
            resources.Add(new AuditResource
            {
                DisplayName = GetString(resource, "displayName"),
                ResourceType = GetString(resource, "type")
            });
        }
        return resources;
    }

    private static int GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return Math.Max(1, (int)delta.TotalSeconds);

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = (int)(date - DateTimeOffset.UtcNow).TotalSeconds;
            return Math.Max(1, wait);
        }

        return DefaultRetryDelaySeconds;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenRequestContext = new TokenRequestContext(_scopes);
        var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
        return token.Token;
    }

    private static HttpClient CreateHttpClient(string bearerToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;

    private static DateTime? TryParseDateTime(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop)
        && prop.ValueKind == JsonValueKind.String
        && DateTime.TryParse(prop.GetString(), out var dt)
            ? dt
            : null;
}
