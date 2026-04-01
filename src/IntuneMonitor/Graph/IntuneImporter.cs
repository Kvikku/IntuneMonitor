using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Graph;

/// <summary>
/// Imports Intune policies into a tenant using Microsoft Graph.
/// </summary>
public class IntuneImporter
{
    private readonly TokenCredential _credential;
    private readonly ILogger<IntuneImporter> _logger;

    /// <summary>Internal hook for tests to provide a custom HttpClient factory.</summary>
    internal Func<CancellationToken, Task<HttpClient>>? HttpClientFactory { get; set; }

    public IntuneImporter(TokenCredential credential, ILoggerFactory? loggerFactory = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<IntuneImporter>();
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        if (HttpClientFactory != null)
            return await HttpClientFactory(cancellationToken);

        var token = await GraphClientFactory.GetAccessTokenAsync(_credential, cancellationToken);
        return GraphClientFactory.CreateHttpClient(token);
    }

    /// <summary>
    /// Imports a single Intune item into the target tenant.
    /// </summary>
    /// <param name="item">The item to import (must have PolicyData set).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The name of the imported policy, or null if the import failed.</returns>
    public async Task<string?> ImportItemAsync(IntuneItem item, CancellationToken cancellationToken = default)
    {
        if (item.PolicyData == null)
            throw new ArgumentException("Item must have PolicyData set to import.", nameof(item));

        if (!IntuneContentTypes.GraphEndpoints.TryGetValue(item.ContentType ?? "", out var endpoint))
            throw new ArgumentException($"Unsupported content type: '{item.ContentType}'", nameof(item));

        // Prepare the payload: remove read-only fields before posting
        var payload = PrepareImportPayload(item.PolicyData.Value);

        var url = $"https://graph.microsoft.com/beta/{endpoint}";

        using var httpClient = await CreateHttpClientAsync(cancellationToken);

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await GraphRetryHandler.PostWithRetryAsync(
            httpClient, url, content, _logger, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to import '{item.Name}' ({response.StatusCode}): {error}");
        }

        return item.Name;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static readonly HashSet<string> ReadOnlyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdDateTime", "lastModifiedDateTime", "version",
        "@odata.context", "@odata.type", "roleScopeTagIds",
        "settingsCount", "isAssigned"
    };

    private static JsonElement PrepareImportPayload(JsonElement source)
    {
        // Build a dictionary, stripping read-only fields
        var dict = new Dictionary<string, object?>();

        foreach (var prop in source.EnumerateObject())
        {
            if (ReadOnlyFields.Contains(prop.Name))
                continue;
            dict[prop.Name] = ConvertJsonElement(prop.Value);
        }

        var json = JsonSerializer.Serialize(dict);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static object? ConvertJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? (object?)l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
