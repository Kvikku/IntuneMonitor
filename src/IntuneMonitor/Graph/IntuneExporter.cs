using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Exports Intune policies from Microsoft Graph using the beta endpoint.
/// </summary>
public class IntuneExporter
{
    private readonly TokenCredential _credential;
    private readonly ILogger<IntuneExporter> _logger;

    /// <summary>Cache of group ID → display name to avoid redundant Graph lookups.</summary>
    private readonly Dictionary<string, string> _groupNameCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Content types that do not support an /assignments sub-resource.</summary>
    private static readonly HashSet<string> NoAssignmentsTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        IntuneContentTypes.AssignmentFilter,
        IntuneContentTypes.ConditionalAccessPolicy,
        IntuneContentTypes.RoleDefinition,
        IntuneContentTypes.NamedLocation,
        IntuneContentTypes.EnrollmentRestriction,
    };

    /// <summary>Well-known virtual group IDs used by Intune.</summary>
    private static readonly Dictionary<string, string> WellKnownGroupIds = new(StringComparer.OrdinalIgnoreCase)
    {
        { "acacacac-9df4-4c7d-9d50-4ef0226f57a9", "All Users" },
        { "adadadad-808e-44e2-905a-0b7873a8a531", "All Devices" }
    };

    /// <summary>Maps assignment target @odata.type to a friendly display name.</summary>
    private static readonly Dictionary<string, string> VirtualTargetNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "#microsoft.graph.allLicensedUsersAssignmentTarget", "All Users" },
        { "#microsoft.graph.allDevicesAssignmentTarget", "All Devices" }
    };

    /// <summary>Internal hook for tests to provide a custom HttpClient factory.</summary>
    internal Func<CancellationToken, Task<HttpClient>>? HttpClientFactory { get; set; }

    public IntuneExporter(TokenCredential credential, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<IntuneExporter>();
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        if (HttpClientFactory != null)
            return await HttpClientFactory(cancellationToken);

        var token = await GraphClientFactory.GetAccessTokenAsync(_credential, cancellationToken);
        return GraphClientFactory.CreateHttpClient(token);
    }

    /// <summary>
    /// Exports all items for the specified content type from Microsoft Graph.
    /// </summary>
    /// <param name="contentType">One of the <see cref="IntuneContentTypes"/> constants.</param>
    /// <param name="progress">Optional progress reporting callback (item name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="IntuneItem"/> with full policy data.</returns>
    public async Task<List<IntuneItem>> ExportContentTypeAsync(
        string contentType,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IntuneContentTypes.GraphEndpoints.TryGetValue(contentType, out var endpoint))
            throw new ArgumentException($"Unsupported content type: '{contentType}'", nameof(contentType));

        using var httpClient = await CreateHttpClientAsync(cancellationToken);

        var items = new List<IntuneItem>();
        var url = $"https://graph.microsoft.com/beta/{endpoint}";

        // Fetch list pages
        while (url != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var root = JsonSerializer.Deserialize<JsonElement>(json);

            if (!root.TryGetProperty("value", out var valueArray))
                break;

            foreach (var item in valueArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var id = JsonElementHelpers.GetStringOrNull(item, "id");
                var name = JsonElementHelpers.GetDisplayName(item);
                var description = JsonElementHelpers.GetStringOrNull(item, "description");
                var platform = JsonElementHelpers.GetStringOrNull(item, "platforms");

                DateTime? lastModified = JsonElementHelpers.TryParseDateTime(item, "lastModifiedDateTime");
                DateTime? created = JsonElementHelpers.TryParseDateTime(item, "createdDateTime");

                progress?.Report($"[{contentType}] {name}");

                // For some content types the list endpoint doesn't return full settings;
                // fetch the individual item detail.
                JsonElement? fullData;
                if (NeedsDetailFetch(contentType) && id != null)
                {
                    fullData = await FetchItemDetailAsync(httpClient, endpoint, id, cancellationToken);

                    // Settings Catalog stores actual settings in a separate /settings sub-resource
                    if (contentType.Equals(IntuneContentTypes.SettingsCatalog, StringComparison.OrdinalIgnoreCase)
                        && fullData != null)
                    {
                        fullData = await MergeSettingsCatalogSettingsAsync(
                            httpClient, endpoint, id, fullData.Value, cancellationToken);
                    }
                }
                else
                {
                    fullData = item;
                }

                // Fetch and merge assignments with resolved group names
                if (fullData != null && id != null && !NoAssignmentsTypes.Contains(contentType))
                {
                    fullData = await MergeAssignmentsAsync(httpClient, endpoint, id, fullData.Value, cancellationToken);
                }

                items.Add(new IntuneItem
                {
                    Id = id,
                    Name = name,
                    Description = description,
                    Platform = platform,
                    ContentType = contentType,
                    LastModifiedDateTime = lastModified,
                    CreatedDateTime = created,
                    PolicyData = fullData
                });
            }

            // OData paging
            url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                ? nextProp.GetString()
                : null;
        }

        return items;
    }

    /// <summary>
    /// Exports all content for all supported (or specified) content types.
    /// </summary>
    public async Task<Dictionary<string, List<IntuneItem>>> ExportAllAsync(
        IEnumerable<string>? contentTypes = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var types = contentTypes?.ToList() ?? IntuneContentTypes.All.ToList();
        var result = new Dictionary<string, List<IntuneItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var contentType in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Fetching {contentType}...");

            try
            {
                var items = await ExportContentTypeAsync(contentType, progress, cancellationToken);
                result[contentType] = items;
                progress?.Report($"  → {items.Count} item(s) fetched for {contentType}");
            }
            catch (Exception ex)
            {
                progress?.Report($"  ⚠ Error fetching {contentType}: {ex.Message}");
                result[contentType] = new List<IntuneItem>();
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<JsonElement?> FetchItemDetailAsync(
        HttpClient httpClient,
        string endpoint,
        string itemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://graph.microsoft.com/beta/{endpoint}/{itemId}";
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch detail for {Endpoint}/{ItemId}", endpoint, itemId);
            return null;
        }
    }

    /// <summary>
    /// Fetches the /settings sub-resource for a Settings Catalog policy and merges
    /// the settings array into the policy JSON as a "settings" property.
    /// </summary>
    private async Task<JsonElement?> MergeSettingsCatalogSettingsAsync(
        HttpClient httpClient,
        string endpoint,
        string itemId,
        JsonElement policyDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            var allSettings = new List<JsonElement>();
            string? url = $"https://graph.microsoft.com/beta/{endpoint}/{itemId}/settings";

            while (url != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonSerializer.Deserialize<JsonElement>(json);

                if (root.TryGetProperty("value", out var valueArray))
                {
                    foreach (var setting in valueArray.EnumerateArray())
                        allSettings.Add(setting);
                }

                url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                    ? nextProp.GetString()
                    : null;
            }

            // Merge: copy all existing properties and add/replace "settings"
            var dict = new Dictionary<string, object?>();
            foreach (var prop in policyDetail.EnumerateObject())
                dict[prop.Name] = prop.Value;

            dict["settings"] = allSettings;

            var merged = JsonSerializer.Serialize(dict);
            return JsonSerializer.Deserialize<JsonElement>(merged);
        }
        catch (Exception ex)
        {
            // If fetching settings fails, return the policy detail as-is
            _logger.LogDebug(ex, "Failed to fetch settings for {Endpoint}/{ItemId}", endpoint, itemId);
            return policyDetail;
        }
    }

    private static bool NeedsDetailFetch(string contentType) =>
        contentType.Equals(IntuneContentTypes.SettingsCatalog, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.PowerShellScript, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.ProactiveRemediation, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.MacOSShellScript, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.EndpointSecurityPolicy, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fetches the /assignments sub-resource for a policy, resolves group IDs to
    /// display names, and merges the enriched assignments into the policy JSON.
    /// </summary>
    private async Task<JsonElement?> MergeAssignmentsAsync(
        HttpClient httpClient,
        string endpoint,
        string itemId,
        JsonElement policyDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            var allAssignments = new List<JsonElement>();
            string? url = $"https://graph.microsoft.com/beta/{endpoint}/{itemId}/assignments";

            while (url != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    break;

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonSerializer.Deserialize<JsonElement>(json);

                if (root.TryGetProperty("value", out var valueArray))
                {
                    foreach (var assignment in valueArray.EnumerateArray())
                        allAssignments.Add(assignment);
                }

                url = root.TryGetProperty("@odata.nextLink", out var nextProp)
                    ? nextProp.GetString()
                    : null;
            }

            // Resolve group IDs to display names
            var enriched = new List<object>();
            foreach (var assignment in allAssignments)
            {
                var dict = JsonElementHelpers.ToDictionary(assignment);

                if (assignment.TryGetProperty("target", out var target))
                {
                    var targetDict = JsonElementHelpers.ToDictionary(target);
                    var groupId = JsonElementHelpers.GetStringOrNull(target, "groupId");
                    var odataType = JsonElementHelpers.GetStringOrNull(target, "@odata.type");

                    if (groupId != null)
                    {
                        var groupName = await ResolveGroupNameAsync(httpClient, groupId, cancellationToken);
                        targetDict["groupDisplayName"] = groupName;
                    }
                    else if (odataType != null && VirtualTargetNames.TryGetValue(odataType, out var virtualName))
                    {
                        targetDict["groupDisplayName"] = virtualName;
                    }

                    dict["target"] = targetDict;
                }

                enriched.Add(dict);
            }

            // Merge into policy data
            var policyDict = new Dictionary<string, object?>();
            foreach (var prop in policyDetail.EnumerateObject())
                policyDict[prop.Name] = prop.Value;

            policyDict["assignments"] = enriched;

            var merged = JsonSerializer.Serialize(policyDict);
            return JsonSerializer.Deserialize<JsonElement>(merged);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch assignments for {Endpoint}/{ItemId}", endpoint, itemId);
            return policyDetail;
        }
    }

    /// <summary>
    /// Resolves a group ID to its display name, with caching.
    /// </summary>
    private async Task<string> ResolveGroupNameAsync(
        HttpClient httpClient,
        string groupId,
        CancellationToken cancellationToken)
    {
        if (_groupNameCache.TryGetValue(groupId, out var cached))
            return cached;

        // Check well-known virtual group IDs first
        if (WellKnownGroupIds.TryGetValue(groupId, out var wellKnown))
        {
            _groupNameCache[groupId] = wellKnown;
            return wellKnown;
        }

        try
        {
            var url = $"https://graph.microsoft.com/v1.0/groups/{groupId}?$select=displayName";
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var group = JsonSerializer.Deserialize<JsonElement>(json);
                var name = JsonElementHelpers.GetStringOrNull(group, "displayName") ?? groupId;
                _groupNameCache[groupId] = name;
                return name;
            }
        }
        catch (Exception ex)
        {
            // Fall through to return the raw ID
            _logger.LogDebug(ex, "Failed to resolve group name for {GroupId}", groupId);
        }

        _groupNameCache[groupId] = groupId;
        return groupId;
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = prop.Value;
        return dict;
    }
}
