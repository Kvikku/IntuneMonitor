using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;

namespace IntuneMonitor.Graph;

/// <summary>
/// Exports Intune policies from Microsoft Graph using the beta endpoint.
/// </summary>
public class IntuneExporter
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes = { "https://graph.microsoft.com/.default" };

    public IntuneExporter(TokenCredential credential)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
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

        var token = await GetAccessTokenAsync(cancellationToken);
        using var httpClient = CreateHttpClient(token);

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

                var id = GetStringProperty(item, "id");
                var name = GetDisplayName(item);
                var description = GetStringProperty(item, "description");
                var platform = GetStringProperty(item, "platforms");

                DateTime? lastModified = TryParseDate(item, "lastModifiedDateTime");
                DateTime? created = TryParseDate(item, "createdDateTime");

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
        catch
        {
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
        catch
        {
            // If fetching settings fails, return the policy detail as-is
            return policyDetail;
        }
    }

    private static bool NeedsDetailFetch(string contentType) =>
        contentType.Equals(IntuneContentTypes.SettingsCatalog, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.PowerShellScript, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.ProactiveRemediation, StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(IntuneContentTypes.MacOSShellScript, StringComparison.OrdinalIgnoreCase);

    private static string? GetStringProperty(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static DateTime? TryParseDate(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop)
        && prop.ValueKind == JsonValueKind.String
        && DateTime.TryParse(prop.GetString(), out var dt)
            ? dt
            : null;

    private static string? GetDisplayName(JsonElement item)
    {
        foreach (var prop in new[] { "displayName", "name", "id" })
        {
            if (item.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        return null;
    }
}
