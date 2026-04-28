using System.Text.Json;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Storage;

/// <summary>
/// Stores and loads Entra app/enterprise app snapshots as timestamped JSON files
/// in a local directory. Each run creates a new timestamped folder.
/// No git or blob sync — purely local history.
/// </summary>
public class EntraSnapshotStorage
{
    private readonly string _basePath;
    private readonly ILogger<EntraSnapshotStorage> _logger;

    private const string AppRegistrationsFile = "app-registrations.json";
    private const string EnterpriseAppsFile = "enterprise-applications.json";
    private const string TimestampFormat = "yyyy-MM-dd_HHmmss";

    public EntraSnapshotStorage(string basePath, ILoggerFactory? loggerFactory = null)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntraSnapshotStorage>();
    }

    /// <summary>
    /// Saves a snapshot of app registrations and enterprise applications.
    /// Returns the path to the created snapshot folder.
    /// </summary>
    public async Task<string> SaveSnapshotAsync(
        EntraSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var folderName = DateTime.UtcNow.ToString(TimestampFormat);
        var folderPath = Path.Combine(_basePath, folderName);
        Directory.CreateDirectory(folderPath);

        var appRegsPath = Path.Combine(folderPath, AppRegistrationsFile);
        var json = JsonSerializer.Serialize(snapshot.AppRegistrations, JsonDefaults.IndentedCamelCase);
        await File.WriteAllTextAsync(appRegsPath, json, cancellationToken);

        var enterpriseAppsPath = Path.Combine(folderPath, EnterpriseAppsFile);
        json = JsonSerializer.Serialize(snapshot.EnterpriseApplications, JsonDefaults.IndentedCamelCase);
        await File.WriteAllTextAsync(enterpriseAppsPath, json, cancellationToken);

        _logger.LogInformation("Entra snapshot saved to {FolderPath}", folderPath);
        return folderPath;
    }

    /// <summary>
    /// Loads the most recent previous snapshot, or null if none exists.
    /// </summary>
    public async Task<EntraSnapshot?> LoadLatestSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_basePath))
            return null;

        var folders = Directory.GetDirectories(_basePath)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .OrderDescending()
            .ToList();

        foreach (var folder in folders)
        {
            var folderPath = Path.Combine(_basePath, folder!);
            var appRegsPath = Path.Combine(folderPath, AppRegistrationsFile);
            var enterpriseAppsPath = Path.Combine(folderPath, EnterpriseAppsFile);

            if (!File.Exists(appRegsPath) || !File.Exists(enterpriseAppsPath))
                continue;

            try
            {
                var appRegsJson = await File.ReadAllTextAsync(appRegsPath, cancellationToken);
                var appRegs = JsonSerializer.Deserialize<List<EntraAppItem>>(appRegsJson, JsonDefaults.CaseInsensitiveRead)
                    ?? new List<EntraAppItem>();

                var enterpriseAppsJson = await File.ReadAllTextAsync(enterpriseAppsPath, cancellationToken);
                var enterpriseApps = JsonSerializer.Deserialize<List<EntraAppItem>>(enterpriseAppsJson, JsonDefaults.CaseInsensitiveRead)
                    ?? new List<EntraAppItem>();

                _logger.LogDebug("Loaded previous Entra snapshot from {FolderPath}", folderPath);
                return new EntraSnapshot
                {
                    SnapshotPath = folderPath,
                    AppRegistrations = appRegs,
                    EnterpriseApplications = enterpriseApps
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Entra snapshot from {FolderPath}, trying next", folderPath);
            }
        }

        return null;
    }
}
