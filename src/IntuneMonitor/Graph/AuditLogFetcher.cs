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

    public AuditLogFetcher(TokenCredential credential, GraphClientFactory graphClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _graphClientFactory = graphClientFactory ?? throw new ArgumentNullException(nameof(graphClientFactory));
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

        var token = await GraphClientFactory.GetAccessTokenAsync(_credential, cancellationToken);
        using var httpClient = _graphClientFactory.CreateHttpClient(token);

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

            var json = await GraphRetryHandler.SendWithRetryAsync(httpClient, url, _logger, cancellationToken);
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

}
