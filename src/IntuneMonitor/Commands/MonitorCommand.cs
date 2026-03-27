using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Comparison;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Commands;

/// <summary>
/// Monitors Intune for changes by comparing current state against the backup.
/// Can run once or on a schedule.
/// </summary>
public class MonitorCommand
{
    private static readonly JsonSerializerOptions ReportWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppConfiguration _config;

    public MonitorCommand(AppConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Runs one monitoring cycle (fetch, compare, report).
    /// </summary>
    public async Task<ChangeReport> RunOnceAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("=== Intune Monitor ===");
        Console.WriteLine($"Started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = CredentialFactory.Create(_config.Authentication);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Authentication error: {ex.Message}");
            return EmptyReport();
        }

        // Determine content types
        var types = ResolveContentTypes(contentTypes);

        // Load storage
        IBackupStorage storage;
        try
        {
            storage = BackupStorageFactory.Create(_config.Backup);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Storage error: {ex.Message}");
            return EmptyReport();
        }

        // Fetch current state from Graph
        var exporter = new IntuneExporter(credential);
        var progress = new Progress<string>(msg => Console.WriteLine($"  {msg}"));

        Dictionary<string, List<IntuneItem>> liveData;
        try
        {
            liveData = await exporter.ExportAllAsync(types, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to fetch data from Graph: {ex.Message}");
            return EmptyReport();
        }

        // Compare against backup
        var comparer = new PolicyComparer();
        var allChanges = new List<PolicyChange>();

        foreach (var (contentType, liveItems) in liveData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var backup = await storage.LoadBackupAsync(contentType, cancellationToken);
            var changes = comparer.Compare(contentType, liveItems, backup);

            if (changes.Count > 0)
            {
                allChanges.AddRange(changes);
            }
        }

        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = _config.Authentication.TenantId,
            TenantName = _config.Authentication.TenantId,
            Changes = allChanges
        };

        PrintReport(report);
        await WriteReportAsync(report, cancellationToken);

        return report;
    }

    /// <summary>
    /// Runs the monitor on a recurring schedule until cancellation is requested.
    /// </summary>
    public async Task RunScheduledAsync(
        IEnumerable<string>? contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        var intervalMinutes = _config.Monitor.IntervalMinutes;
        if (intervalMinutes <= 0)
        {
            // Single run
            await RunOnceAsync(contentTypes, cancellationToken);
            return;
        }

        Console.WriteLine($"Scheduled monitoring: running every {intervalMinutes} minute(s). Press Ctrl+C to stop.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(contentTypes, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Monitor run failed: {ex.Message}");
            }

            Console.WriteLine($"Next run in {intervalMinutes} minute(s). Waiting...");
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine("Monitoring stopped.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void PrintReport(ChangeReport report)
    {
        if (!report.HasChanges)
        {
            if (!_config.Monitor.ChangesOnly)
                Console.WriteLine("\n✓ No changes detected.");
            return;
        }

        Console.WriteLine($"\n{'=',-60}");
        Console.WriteLine($"CHANGE REPORT – {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Tenant: {report.TenantName}");
        Console.WriteLine($"Changes: {report.AddedCount} added, {report.RemovedCount} removed, {report.ModifiedCount} modified");
        Console.WriteLine($"{'=',-60}");

        var minSeverity = ParseSeverity(_config.Monitor.MinSeverity);

        foreach (var change in report.Changes)
        {
            if (change.Severity < minSeverity) continue;

            var icon = change.ChangeType switch
            {
                ChangeType.Added => "✚",
                ChangeType.Removed => "✖",
                ChangeType.Modified => "✎",
                _ => "?"
            };

            Console.WriteLine($"\n{icon} [{change.ContentType}] {change.PolicyName} ({change.ChangeType})");
            Console.WriteLine($"  ID: {change.PolicyId}");

            if (!string.IsNullOrEmpty(change.Details))
                Console.WriteLine($"  Details: {change.Details}");

            foreach (var field in change.FieldChanges.Take(10))
            {
                if (field.FieldPath.StartsWith("assignments.", StringComparison.OrdinalIgnoreCase))
                {
                    // Human-readable assignment change
                    var action = field.FieldPath.Split('.').Last();
                    if (action.Equals("added", StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine($"  Assignment added: {field.NewValue}");
                    else if (action.Equals("removed", StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine($"  Assignment removed: {field.OldValue}");
                    else if (action.Equals("modified", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  Assignment modified:");
                        Console.WriteLine($"    Before: {field.OldValue}");
                        Console.WriteLine($"    After:  {field.NewValue}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Field: {field.FieldPath}");
                    Console.WriteLine($"    Before: {Truncate(field.OldValue, 120)}");
                    Console.WriteLine($"    After:  {Truncate(field.NewValue, 120)}");
                }
            }

            if (change.FieldChanges.Count > 10)
                Console.WriteLine($"  ... and {change.FieldChanges.Count - 10} more field change(s).");
        }

        Console.WriteLine($"\n{'=',-60}");
    }

    private async Task WriteReportAsync(ChangeReport report, CancellationToken cancellationToken)
    {
        var outputPath = _config.Monitor.ReportOutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(report, ReportWriteOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            Console.WriteLine($"Report written to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to write report to '{outputPath}': {ex.Message}");
        }
    }

    private List<string> ResolveContentTypes(IEnumerable<string>? specified)
    {
        if (specified != null)
        {
            var list = specified.ToList();
            if (list.Count > 0) return list;
        }

        if (_config.ContentTypes?.Count > 0)
            return _config.ContentTypes;

        return IntuneContentTypes.All.ToList();
    }

    private static ChangeReport EmptyReport() =>
        new() { GeneratedAt = DateTime.UtcNow };

    private static ChangeSeverity ParseSeverity(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "critical" => ChangeSeverity.Critical,
            "warning" => ChangeSeverity.Warning,
            _ => ChangeSeverity.Info
        };

    private static string? Truncate(string? value, int maxLength) =>
        value == null ? "(null)" :
        value.Length <= maxLength ? value :
        value[..maxLength] + "...";
}
