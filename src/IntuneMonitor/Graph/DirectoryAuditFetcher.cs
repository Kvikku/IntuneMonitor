using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Fetches directory audit events from Microsoft Graph for Entra ID app registration
/// and enterprise application changes (owner modifications, permission changes).
/// </summary>
public class DirectoryAuditFetcher
{
    private readonly TokenCredential _credential;
    private readonly GraphClientFactory _graphClientFactory;
    private readonly ILogger<DirectoryAuditFetcher> _logger;

    /// <summary>Maximum number of events per page to request from Graph.</summary>
    private const int PageSize = 100;

    /// <summary>Small delay between page requests to reduce throttling risk.</summary>
    private static readonly TimeSpan PageRequestDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Directory audit activity names that indicate app registration or enterprise application changes.
    /// </summary>
    internal static readonly IReadOnlyList<string> MonitoredActivities = new[]
    {
        "Add owner to application",
        "Remove owner from application",
        "Add owner to service principal",
        "Remove owner from service principal",
        "Add app role assignment to service principal",
        "Remove app role assignment grant",
        "Add delegated permission grant",
        "Remove delegated permission grant",
        "Consent to application",
        "Update application – Certificates and secrets management",
        "Update application",
        "Update service principal",
    };

    public DirectoryAuditFetcher(TokenCredential credential, GraphClientFactory graphClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _graphClientFactory = graphClientFactory ?? throw new ArgumentNullException(nameof(graphClientFactory));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DirectoryAuditFetcher>();
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
    /// Fetches directory audit events related to app registrations and enterprise applications
    /// for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to look back (1–30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of directory audit events matching monitored activities.</returns>
    public async Task<List<DirectoryAuditEvent>> FetchDirectoryAuditEventsAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 30)
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be between 1 and 30.");

        using var httpClient = await CreateHttpClientAsync(cancellationToken);

        var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Build OData filter for monitored activities
        var activityFilter = string.Join(" or ",
            MonitoredActivities.Select(a => $"activityDisplayName eq '{a}'"));
        var filter = Uri.EscapeDataString($"activityDateTime ge {since} and ({activityFilter})");
        var orderby = Uri.EscapeDataString("activityDateTime desc");

        var url = $"{GraphClientFactory.GraphV1BaseUrl}/auditLogs/directoryAudits"
                + $"?$filter={filter}"
                + $"&$orderby={orderby}"
                + $"&$top={PageSize}";

        var events = new List<DirectoryAuditEvent>();
        int pageCount = 0;

        while (url != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pageCount++;

            _logger.LogDebug("Fetching directory audit page {PageNumber}...", pageCount);

            var json = await GraphRetryHandler.SendWithRetryAsync(httpClient, url, _logger, cancellationToken, delayFunc: DelayFunc);
            if (json == null)
                break;

            var root = JsonSerializer.Deserialize<JsonElement>(json);

            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    var auditEvent = ParseDirectoryAuditEvent(item);
                    if (auditEvent != null)
                        events.Add(auditEvent);
                }

                _logger.LogDebug("Page {PageNumber}: {Count} event(s) fetched", pageCount, valueArray.GetArrayLength());
            }

            // OData paging
            url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                ? nextProp.GetString()
                : null;

            if (url != null)
                await DelayAsync(PageRequestDelay, cancellationToken);
        }

        _logger.LogInformation("Fetched {TotalEvents} directory audit event(s) across {PageCount} page(s)", events.Count, pageCount);
        return events;
    }

    /// <summary>
    /// Parses a JSON element into a <see cref="DirectoryAuditEvent"/>.
    /// </summary>
    private DirectoryAuditEvent? ParseDirectoryAuditEvent(JsonElement item)
    {
        try
        {
            return new DirectoryAuditEvent
            {
                Id = JsonElementHelpers.GetStringOrEmpty(item, "id"),
                ActivityDisplayName = JsonElementHelpers.GetStringOrEmpty(item, "activityDisplayName"),
                ActivityDateTime = JsonElementHelpers.GetStringOrEmpty(item, "activityDateTime"),
                Category = JsonElementHelpers.GetStringOrEmpty(item, "category"),
                Result = JsonElementHelpers.GetStringOrEmpty(item, "result"),
                OperationType = JsonElementHelpers.GetStringOrEmpty(item, "operationType"),
                CorrelationId = JsonElementHelpers.GetStringOrEmpty(item, "correlationId"),
                InitiatedBy = ParseInitiatedBy(item),
                TargetResources = ParseTargetResources(item)
            };
        }
        catch (Exception ex)
        {
            var eventId = JsonElementHelpers.GetStringOrEmpty(item, "id");
            _logger.LogWarning(ex, "Failed to parse directory audit event (id: {EventId})", eventId);
            return null;
        }
    }

    private static DirectoryAuditActor? ParseInitiatedBy(JsonElement item)
    {
        if (!item.TryGetProperty("initiatedBy", out var initiatedBy) || initiatedBy.ValueKind != JsonValueKind.Object)
            return null;

        // initiatedBy has "user" and/or "app" sub-objects
        string? upn = null, userDisplayName = null, userId = null, ipAddress = null;
        string? appDisplayName = null, appId = null;

        if (initiatedBy.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            upn = JsonElementHelpers.GetStringOrNull(user, "userPrincipalName");
            userDisplayName = JsonElementHelpers.GetStringOrNull(user, "displayName");
            userId = JsonElementHelpers.GetStringOrNull(user, "id");
            ipAddress = JsonElementHelpers.GetStringOrNull(user, "ipAddress");
        }

        if (initiatedBy.TryGetProperty("app", out var app) && app.ValueKind == JsonValueKind.Object)
        {
            appDisplayName = JsonElementHelpers.GetStringOrNull(app, "displayName");
            appId = JsonElementHelpers.GetStringOrNull(app, "appId");
        }

        return new DirectoryAuditActor
        {
            UserPrincipalName = upn,
            UserDisplayName = userDisplayName,
            UserId = userId,
            IpAddress = ipAddress,
            AppDisplayName = appDisplayName,
            AppId = appId
        };
    }

    private static List<DirectoryAuditTarget> ParseTargetResources(JsonElement item)
    {
        var targets = new List<DirectoryAuditTarget>();
        if (!item.TryGetProperty("targetResources", out var resourcesArray)
            || resourcesArray.ValueKind != JsonValueKind.Array)
            return targets;

        foreach (var resource in resourcesArray.EnumerateArray())
        {
            targets.Add(new DirectoryAuditTarget
            {
                Id = JsonElementHelpers.GetStringOrEmpty(resource, "id"),
                DisplayName = JsonElementHelpers.GetStringOrEmpty(resource, "displayName"),
                Type = JsonElementHelpers.GetStringOrEmpty(resource, "type"),
                ModifiedProperties = ParseModifiedProperties(resource)
            });
        }
        return targets;
    }

    private static List<DirectoryAuditModifiedProperty> ParseModifiedProperties(JsonElement resource)
    {
        var props = new List<DirectoryAuditModifiedProperty>();
        if (!resource.TryGetProperty("modifiedProperties", out var propsArray)
            || propsArray.ValueKind != JsonValueKind.Array)
            return props;

        foreach (var prop in propsArray.EnumerateArray())
        {
            props.Add(new DirectoryAuditModifiedProperty
            {
                DisplayName = JsonElementHelpers.GetStringOrEmpty(prop, "displayName"),
                OldValue = JsonElementHelpers.GetStringOrNull(prop, "oldValue"),
                NewValue = JsonElementHelpers.GetStringOrNull(prop, "newValue")
            });
        }
        return props;
    }
}
