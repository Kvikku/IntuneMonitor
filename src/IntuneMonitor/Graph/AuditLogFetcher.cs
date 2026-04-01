using System.Net;
using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Fetches Intune audit events from Microsoft Graph with throttling support.
/// Uses <see cref="GraphRetryHandler"/> for retry logic on transient failures.
/// </summary>
public class AuditLogFetcher
{
    private readonly TokenCredential _credential;
    private readonly GraphClientFactory _graphClientFactory;
    private readonly ILogger<AuditLogFetcher> _logger;

    /// <summary>Maximum number of events per page to request from Graph.</summary>
    private const int PageSize = 100;

    /// <summary>Small delay between page requests to reduce throttling risk.</summary>
    private static readonly TimeSpan PageRequestDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Maximum number of retry attempts for transient failures.</summary>
    private const int MaxAttempts = 5;

    /// <summary>Default delay in seconds when no Retry-After header is present on a 429 response.</summary>
    private const int DefaultRetryDelaySeconds = 30;

    /// <summary>Base delay in seconds for exponential backoff on server errors.</summary>
    private const int BaseBackoffSeconds = 5;

    public AuditLogFetcher(TokenCredential credential, GraphClientFactory graphClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _graphClientFactory = graphClientFactory ?? throw new ArgumentNullException(nameof(graphClientFactory));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AuditLogFetcher>();
    }

    /// <summary>Internal hook for tests to provide a custom HttpClient factory.</summary>
    internal Func<CancellationToken, Task<HttpClient>>? HttpClientFactory { get; set; }

    /// <summary>Internal hook for tests to replace Task.Delay with a no-op or fast implementation.</summary>
    internal Func<TimeSpan, CancellationToken, Task>? DelayFunc { get; set; }

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        DelayFunc != null
            ? DelayFunc(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        if (HttpClientFactory != null)
            return await HttpClientFactory(cancellationToken);

        var token = await GraphClientFactory.GetAccessTokenAsync(_credential, cancellationToken);
        return _graphClientFactory.CreateHttpClient(token);
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

        using var httpClient = await CreateHttpClientAsync(cancellationToken);

        var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var filter = Uri.EscapeDataString($"activityDateTime ge {since}");
        var orderby = Uri.EscapeDataString("activityDateTime desc");
        var url = $"https://graph.microsoft.com/beta/deviceManagement/auditEvents"
                + $"?$filter={filter}"
                + $"&$orderby={orderby}"
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
                await DelayAsync(PageRequestDelay, cancellationToken);
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
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts - 1)
            {
                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{MaxAttempts}), retrying...", attempt + 1, MaxAttempts);
                await DelayAsync(TimeSpan.FromSeconds(DefaultRetryDelaySeconds), cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts - 1)
                {
                    var retryAfter = GetRetryAfterSeconds(response);
                    _logger.LogWarning("Throttled (HTTP 429). Waiting {RetryAfterSeconds}s before retry (attempt {Attempt}/{MaxAttempts})",
                        retryAfter, attempt + 1, MaxAttempts);
                    await DelayAsync(TimeSpan.FromSeconds(retryAfter), cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < MaxAttempts - 1)
                {
                    var delay = (int)Math.Pow(2, attempt) * BaseBackoffSeconds;
                    _logger.LogWarning("Server error (HTTP {StatusCode}). Retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                        (int)response.StatusCode, delay, attempt + 1, MaxAttempts);
                    await DelayAsync(TimeSpan.FromSeconds(delay), cancellationToken);
                    continue;
                }

                // Non-retryable error
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Audit log request failed with HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }
        }

        _logger.LogError("Audit log request failed after {MaxAttempts} attempts", MaxAttempts);
        return null;
    }

    /// <summary>
    /// Parses a JSON element into an <see cref="AuditEvent"/>.
    /// </summary>
    private AuditEvent? ParseAuditEvent(JsonElement item)
    {
        try
        {
            return new AuditEvent
            {
                Id = JsonElementHelpers.GetStringOrEmpty(item, "id"),
                DisplayName = JsonElementHelpers.GetStringOrEmpty(item, "displayName"),
                ComponentName = JsonElementHelpers.GetStringOrEmpty(item, "componentName"),
                Activity = JsonElementHelpers.GetStringOrEmpty(item, "activity"),
                ActivityType = JsonElementHelpers.GetStringOrEmpty(item, "activityType"),
                ActivityResult = JsonElementHelpers.GetStringOrEmpty(item, "activityResult"),
                ActivityDateTime = JsonElementHelpers.TryParseDateTime(item, "activityDateTime") ?? DateTime.MinValue,
                Actor = ParseActor(item),
                Resources = ParseResources(item)
            };
        }
        catch (Exception ex)
        {
            var eventId = JsonElementHelpers.GetStringOrEmpty(item, "id");
            _logger.LogWarning(ex, "Failed to parse audit event (id: {EventId})", eventId);
            return null;
        }
    }

    private static AuditActor? ParseActor(JsonElement item)
    {
        if (!item.TryGetProperty("actor", out var actor) || actor.ValueKind != JsonValueKind.Object)
            return null;

        return new AuditActor
        {
            ApplicationDisplayName = JsonElementHelpers.GetStringOrEmpty(actor, "applicationDisplayName"),
            UserPrincipalName = JsonElementHelpers.GetStringOrEmpty(actor, "userPrincipalName"),
            IpAddress = JsonElementHelpers.GetStringOrEmpty(actor, "ipAddress")
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
                DisplayName = JsonElementHelpers.GetStringOrEmpty(resource, "displayName"),
                ResourceType = JsonElementHelpers.GetStringOrEmpty(resource, "type")
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

}
