using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Exports Entra app registrations and enterprise applications (service principals)
/// from Microsoft Graph, including owners and permission sub-resources.
/// Standalone from IntuneExporter — this data lives in its own pipeline.
/// </summary>
public class EntraExporter
{
    private readonly TokenCredential _credential;
    private readonly GraphClientFactory _graphClientFactory;
    private readonly ILogger<EntraExporter> _logger;

    private const string ApplicationsEndpoint = "applications";
    private const string ServicePrincipalsEndpoint = "servicePrincipals";

    /// <summary>Small delay between per-item sub-resource requests to reduce throttling risk.</summary>
    private static readonly TimeSpan SubResourceDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Internal hook for tests to provide a custom HttpClient factory.</summary>
    internal Func<CancellationToken, Task<HttpClient>>? HttpClientFactory { get; set; }

    /// <summary>Internal hook for tests to replace Task.Delay.</summary>
    internal Func<TimeSpan, CancellationToken, Task>? DelayFunc { get; set; }

    public EntraExporter(TokenCredential credential, GraphClientFactory graphClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _graphClientFactory = graphClientFactory ?? throw new ArgumentNullException(nameof(graphClientFactory));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntraExporter>();
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        if (HttpClientFactory != null)
            return await HttpClientFactory(cancellationToken);

        var token = await GraphClientFactory.GetAccessTokenAsync(_credential, cancellationToken);
        return _graphClientFactory.CreateHttpClient(token);
    }

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        DelayFunc != null
            ? DelayFunc(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    /// <summary>
    /// Fetches all app registrations and enterprise applications with their sub-resources.
    /// </summary>
    public async Task<EntraSnapshot> ExportSnapshotAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = await CreateHttpClientAsync(cancellationToken);

        progress?.Report("Fetching app registrations...");
        var appRegs = await FetchItemsWithExpandAsync(
            httpClient, ApplicationsEndpoint, "AppRegistration",
            new[] { "owners" }, progress, cancellationToken);

        // Service principals only support $expand on one property at a time,
        // so we make two paginated requests and merge the results by ID.
        progress?.Report("Fetching enterprise applications (owners)...");
        var enterpriseApps = await FetchItemsWithExpandAsync(
            httpClient, ServicePrincipalsEndpoint, "EnterpriseApplication",
            new[] { "owners" }, progress, cancellationToken);

        progress?.Report("Fetching enterprise application role assignments...");
        var appRoleItems = await FetchItemsWithExpandAsync(
            httpClient, ServicePrincipalsEndpoint, "EnterpriseApplication",
            new[] { "appRoleAssignments" }, progress, cancellationToken);

        // Merge appRoleAssignments into the main enterprise app items
        var roleAssignmentsBySpId = new Dictionary<string, JsonElement>();
        foreach (var item in appRoleItems)
        {
            if (item.Data.HasValue && item.Data.Value.TryGetProperty("appRoleAssignments", out var assignments))
                roleAssignmentsBySpId[item.Id] = assignments.Clone();
        }

        for (int i = 0; i < enterpriseApps.Count; i++)
        {
            var app = enterpriseApps[i];
            if (roleAssignmentsBySpId.TryGetValue(app.Id, out var assignments) && app.Data != null)
            {
                var dict = JsonElementHelpers.ToDictionary(app.Data.Value);
                dict["appRoleAssignments"] = JsonSerializer.Deserialize<JsonElement>(assignments.GetRawText());
                var merged = JsonSerializer.Serialize(dict);
                enterpriseApps[i] = app with { Data = JsonSerializer.Deserialize<JsonElement>(merged) };
            }
        }

        // Fetch oauth2PermissionGrants per enterprise app (not supported via $expand)
        progress?.Report("Fetching oauth2 permission grants...");
        for (int i = 0; i < enterpriseApps.Count; i++)
        {
            var app = enterpriseApps[i];
            if (app.Data != null)
            {
                var grants = await FetchOAuth2PermissionGrantsAsync(httpClient, app.Id, cancellationToken);
                if (grants.Count > 0)
                {
                    var dict = JsonElementHelpers.ToDictionary(app.Data.Value);
                    dict["oauth2PermissionGrants"] = grants;
                    var merged = JsonSerializer.Serialize(dict);
                    enterpriseApps[i] = app with { Data = JsonSerializer.Deserialize<JsonElement>(merged) };
                }
            }

            // Pace requests to avoid throttling with large tenants
            if (i < enterpriseApps.Count - 1)
                await DelayAsync(SubResourceDelay, cancellationToken);
        }

        _logger.LogInformation("Exported {AppRegCount} app registration(s) and {EACount} enterprise application(s)",
            appRegs.Count, enterpriseApps.Count);

        return new EntraSnapshot
        {
            AppRegistrations = appRegs,
            EnterpriseApplications = enterpriseApps
        };
    }

    /// <summary>
    /// Fetches a paginated list of items from a Graph endpoint using $expand to inline sub-resources.
    /// This avoids per-item sub-resource calls, dramatically reducing the number of HTTP requests.
    /// </summary>
    private async Task<List<EntraAppItem>> FetchItemsWithExpandAsync(
        HttpClient httpClient,
        string endpoint,
        string category,
        string[] expandProperties,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var items = new List<EntraAppItem>();
        var expand = string.Join(",", expandProperties);
        string? url = $"{GraphClientFactory.GraphBetaBaseUrl}/{endpoint}?$expand={expand}";

        while (url != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await GraphRetryHandler.SendWithRetryAsync(httpClient, url, _logger, cancellationToken);
            if (json == null) break;

            var root = JsonSerializer.Deserialize<JsonElement>(json);
            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var raw in valueArray.EnumerateArray())
                {
                    var id = JsonElementHelpers.GetStringOrNull(raw, "id");
                    var displayName = JsonElementHelpers.GetDisplayName(raw) ?? string.Empty;
                    var appId = JsonElementHelpers.GetStringOrNull(raw, "appId");
                    var created = JsonElementHelpers.TryParseDateTime(raw, "createdDateTime");

                    progress?.Report($"[{category}] {displayName}");

                    items.Add(new EntraAppItem
                    {
                        Id = id ?? string.Empty,
                        DisplayName = displayName,
                        AppId = appId,
                        CreatedDateTime = created,
                        Data = raw.Clone()
                    });
                }
            }

            url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                ? nextProp.GetString()
                : null;
        }

        return items;
    }

    /// <summary>
    /// Fetches oauth2PermissionGrants for a service principal via the top-level endpoint.
    /// </summary>
    private async Task<List<JsonElement>> FetchOAuth2PermissionGrantsAsync(
        HttpClient httpClient,
        string servicePrincipalId,
        CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        var filter = Uri.EscapeDataString($"clientId eq '{servicePrincipalId}'");
        string? url = $"{GraphClientFactory.GraphBetaBaseUrl}/oauth2PermissionGrants?$filter={filter}";

        while (url != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await GraphRetryHandler.SendWithRetryAsync(httpClient, url, _logger, cancellationToken);
            if (json == null) break;

            var root = JsonSerializer.Deserialize<JsonElement>(json);
            if (root.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                    results.Add(item.Clone());
            }

            url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                ? nextProp.GetString()
                : null;
        }

        return results;
    }
}
