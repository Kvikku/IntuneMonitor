using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;

namespace IntuneMonitor.Storage;

/// <summary>
/// Stores backups in Azure Blob Storage using the REST API.
/// Authenticates via DefaultAzureCredential or a SAS token URL.
/// </summary>
public class AzureBlobStorage : IBackupStorage
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BackupConfig _config;
    private readonly ILogger<AzureBlobStorage> _logger;
    private readonly string _storageAccountUrl;
    private readonly string _containerName;
    private readonly string? _sasToken;
    private readonly TokenCredential? _credential;

    public AzureBlobStorage(BackupConfig config, ILogger<AzureBlobStorage> logger)
    {
        _config = config;
        _logger = logger;

        var connectionString = config.AzureBlobConnectionString ?? "";

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Backup.AzureBlobConnectionString is required when StorageType is 'AzureBlob'. " +
                "Provide a storage account URL (e.g., 'https://account.blob.core.windows.net').");

        _containerName = config.AzureBlobContainerName ?? "intune-backup";

        var uri = new Uri(connectionString);
        if (!string.IsNullOrWhiteSpace(uri.Query) && uri.Query.Contains("sig="))
        {
            _storageAccountUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            _sasToken = uri.Query;
            _logger.LogDebug("Using SAS token authentication for Azure Blob Storage");

            if (uri.AbsolutePath.Length > 1)
            {
                _containerName = uri.AbsolutePath.Trim('/');
                _storageAccountUrl = $"{uri.Scheme}://{uri.Host}";
            }
        }
        else
        {
            _storageAccountUrl = connectionString.TrimEnd('/');
            _credential = new DefaultAzureCredential();
            _logger.LogDebug("Using DefaultAzureCredential for Azure Blob Storage");
        }
    }

    /// <inheritdoc/>
    public async Task SaveBackupAsync(string contentType, BackupDocument document, CancellationToken cancellationToken = default)
    {
        if (!IntuneContentTypes.FolderNames.TryGetValue(contentType, out var folderName))
            folderName = contentType;

        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var blobPath = string.IsNullOrWhiteSpace(_config.SubDirectory)
            ? $"{folderName}/{fileName}"
            : $"{_config.SubDirectory}/{folderName}/{fileName}";

        blobPath = SanitizeBlobPath(blobPath);

        var json = JsonSerializer.Serialize(document, WriteOptions);

        using var httpClient = await CreateHttpClientAsync(cancellationToken);
        var url = $"{_storageAccountUrl}/{_containerName}/{blobPath}{_sasToken ?? ""}";

        await EnsureContainerExistsAsync(httpClient, cancellationToken);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("x-ms-blob-type", "BlockBlob");

        var response = await httpClient.PutAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to upload blob '{BlobPath}': {StatusCode} {Error}",
                blobPath, response.StatusCode, error);
            throw new InvalidOperationException($"Failed to upload blob '{blobPath}': {response.StatusCode}");
        }

        _logger.LogDebug("Uploaded backup blob: {BlobPath}", blobPath);
    }

    /// <inheritdoc/>
    public async Task<BackupDocument?> LoadBackupAsync(string contentType, CancellationToken cancellationToken = default)
    {
        if (!IntuneContentTypes.FolderNames.TryGetValue(contentType, out var folderName))
            folderName = contentType;

        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var blobPath = string.IsNullOrWhiteSpace(_config.SubDirectory)
            ? $"{folderName}/{fileName}"
            : $"{_config.SubDirectory}/{folderName}/{fileName}";

        blobPath = SanitizeBlobPath(blobPath);

        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var url = $"{_storageAccountUrl}/{_containerName}/{blobPath}{_sasToken ?? ""}";

            var response = await httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No backup blob found for {ContentType}", contentType);
                return null;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<BackupDocument>(json, ReadOptions);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup blob for {ContentType}", contentType);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListStoredContentTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = new List<string>();

        try
        {
            using var httpClient = await CreateHttpClientAsync(cancellationToken);
            var prefix = string.IsNullOrWhiteSpace(_config.SubDirectory) ? "" : $"{_config.SubDirectory}/";
            var url = $"{_storageAccountUrl}/{_containerName}?restype=container&comp=list&prefix={Uri.EscapeDataString(prefix)}&delimiter=/{_sasToken?.Replace("?", "&") ?? ""}";

            var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return types;

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);

            foreach (var contentType in IntuneContentTypes.All)
            {
                if (IntuneContentTypes.FolderNames.TryGetValue(contentType, out var folderName)
                    && xml.Contains(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    types.Add(contentType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list stored content types from Azure Blob Storage");
        }

        return types;
    }

    /// <inheritdoc/>
    public Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Azure Blob Storage export finalized: {CommitMessage}", commitMessage);
        return Task.CompletedTask;
    }

    private async Task<HttpClient> CreateHttpClientAsync(CancellationToken cancellationToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-ms-version", "2023-11-03");

        if (_credential != null)
        {
            var tokenRequestContext = new TokenRequestContext(new[] { "https://storage.azure.com/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        return client;
    }

    private async Task EnsureContainerExistsAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_storageAccountUrl}/{_containerName}?restype=container{_sasToken?.Replace("?", "&") ?? ""}";
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
                _logger.LogDebug("Container '{ContainerName}' is ready", _containerName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Container check/create for '{ContainerName}' failed (may already exist)", _containerName);
        }
    }

    /// <summary>
    /// Sanitizes a blob path segment to prevent path traversal attacks.
    /// Removes ".." segments and normalizes the path.
    /// </summary>
    private static string SanitizeBlobPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safe = segments.Where(s => s != ".." && s != ".").ToArray();
        return string.Join("/", safe);
    }
}
