using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Comparison;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Notifications;
using IntuneMonitor.Reporting;
using IntuneMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Monitors Entra app registrations and enterprise applications for changes.
/// Combines snapshot comparison (drift detection) with directory audit log events
/// into a single nightly report.
/// </summary>
public class EntraMonitorCommand
{
    private readonly AppConfiguration _config;
    private readonly ILogger<EntraMonitorCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public EntraMonitorCommand(AppConfiguration config, IHttpClientFactory httpClientFactory, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<EntraMonitorCommand>();
    }

    /// <summary>
    /// Runs the Entra monitor: fetches current state, compares to previous snapshot,
    /// fetches audit logs, and generates a combined report.
    /// </summary>
    public async Task<EntraMonitorReport> RunAsync(
        int days = 1,
        string? snapshotPath = null,
        string? htmlReportPath = null,
        string? jsonReportPath = null,
        string? mdReportPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Entra App Monitor ===");

        // Authenticate
        TokenCredential credential;
        try
        {
            credential = CredentialFactory.Create(_config.Authentication);
            _logger.LogInformation("Authentication configured (method: {AuthMethod})", _config.Authentication.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            return EmptyReport(days);
        }

        var graphFactory = new GraphClientFactory(_httpClientFactory);
        var storagePath = snapshotPath ?? _config.EntraMonitor.SnapshotPath;
        var storage = new EntraSnapshotStorage(storagePath, _loggerFactory);

        // 1. Load previous snapshot
        _logger.LogInformation("Loading previous Entra snapshot...");
        var previousSnapshot = await storage.LoadLatestSnapshotAsync(cancellationToken);
        if (previousSnapshot == null)
            _logger.LogInformation("No previous snapshot found — this will be the baseline");

        // 2. Fetch current state
        _logger.LogInformation("Fetching current Entra state from Graph...");
        var exporter = new EntraExporter(credential, graphFactory, _loggerFactory);
        EntraSnapshot currentSnapshot;
        try
        {
            currentSnapshot = await exporter.ExportSnapshotAsync(null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Entra state from Graph");
            return EmptyReport(days);
        }

        // 3. Save current snapshot
        var savedPath = await storage.SaveSnapshotAsync(currentSnapshot, cancellationToken);
        _logger.LogInformation("Snapshot saved to {Path}", savedPath);

        // 4. Compare snapshots
        var snapshotChanges = EntraSnapshotComparer.Compare(currentSnapshot, previousSnapshot);
        _logger.LogInformation("Snapshot comparison: {ChangeCount} change(s) detected", snapshotChanges.Count);

        // 5. Fetch directory audit logs
        _logger.LogInformation("Fetching directory audit logs for last {Days} day(s)...", days);
        var auditFetcher = new DirectoryAuditFetcher(credential, graphFactory, _loggerFactory);
        List<DirectoryAuditEvent> auditEvents;
        try
        {
            auditEvents = await auditFetcher.FetchDirectoryAuditEventsAsync(days, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch directory audit logs");
            auditEvents = new List<DirectoryAuditEvent>();
        }

        // 6. Build combined report
        var auditReport = BuildAuditReport(auditEvents, days);
        var report = new EntraMonitorReport
        {
            TenantId = _config.Authentication.TenantId,
            SnapshotChanges = snapshotChanges,
            AuditReport = auditReport
        };

        // 7. Print summary
        PrintSummary(report);

        // 8. Write reports
        var reportTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        await WriteReportsAsync(report, htmlReportPath, jsonReportPath, mdReportPath, reportTimestamp, cancellationToken);

        // 9. Send notifications if configured
        await SendNotificationsAsync(report, cancellationToken);

        return report;
    }

    private static EntraAuditReport BuildAuditReport(List<DirectoryAuditEvent> events, int days)
    {
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddDays(-days);

        var byActivity = events
            .Where(e => !string.IsNullOrEmpty(e.ActivityDisplayName))
            .GroupBy(e => e.ActivityDisplayName)
            .ToDictionary(g => g.Key, g => g.Count());

        var byActor = events
            .Select(GetActorName)
            .Where(name => !string.IsNullOrEmpty(name))
            .GroupBy(name => name!)
            .ToDictionary(g => g.Key, g => g.Count());

        var byTarget = events
            .SelectMany(e => e.TargetResources)
            .Where(t => !string.IsNullOrEmpty(t.DisplayName))
            .GroupBy(t => t.DisplayName)
            .ToDictionary(g => g.Key, g => g.Count());

        return new EntraAuditReport
        {
            DaysReviewed = days,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalEvents = events.Count,
            Events = events,
            EventsByActivity = byActivity,
            EventsByActor = byActor,
            EventsByTargetApp = byTarget
        };
    }

    private static string? GetActorName(DirectoryAuditEvent evt)
    {
        if (evt.InitiatedBy == null) return null;
        return evt.InitiatedBy.UserPrincipalName
            ?? evt.InitiatedBy.UserDisplayName
            ?? evt.InitiatedBy.AppDisplayName;
    }

    private void PrintSummary(EntraMonitorReport report)
    {
        _logger.LogInformation("─────────────────────────────────────────────");
        _logger.LogInformation("Entra Monitor Summary");
        _logger.LogInformation("─────────────────────────────────────────────");
        _logger.LogInformation("Snapshot changes: {Count}", report.TotalSnapshotChanges);
        _logger.LogInformation("Audit log events: {Count}", report.AuditReport.TotalEvents);

        if (report.SnapshotChanges.Count > 0)
        {
            _logger.LogInformation("  Snapshot changes:");
            foreach (var change in report.SnapshotChanges)
            {
                _logger.LogInformation("    [{ChangeType}] {Category} - {AppName}: {Details}",
                    change.ChangeType, change.Category, change.AppName, change.Details);
            }
        }

        if (report.AuditReport.EventsByActivity.Count > 0)
        {
            _logger.LogInformation("  Audit events by activity:");
            foreach (var (activity, count) in report.AuditReport.EventsByActivity.OrderByDescending(kv => kv.Value))
            {
                _logger.LogInformation("    {Activity}: {Count}", activity, count);
            }
        }

        _logger.LogInformation("─────────────────────────────────────────────");
    }

    private async Task WriteReportsAsync(
        EntraMonitorReport report,
        string? htmlPath,
        string? jsonPath,
        string? mdPath,
        string reportTimestamp,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            var path = ReportPath.WithTimestamp(jsonPath, reportTimestamp);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(report, JsonDefaults.IndentedCamelCase);
            await File.WriteAllTextAsync(path, json, cancellationToken);
            _logger.LogInformation("JSON report written to {Path}", path);
        }

        if (!string.IsNullOrWhiteSpace(htmlPath))
        {
            var path = ReportPath.WithTimestamp(htmlPath, reportTimestamp);
            await HtmlEntraReportGenerator.WriteAsync(report, path, cancellationToken);
            _logger.LogInformation("HTML report written to {Path}", path);
        }

        if (!string.IsNullOrWhiteSpace(mdPath))
        {
            var path = ReportPath.WithTimestamp(mdPath, reportTimestamp);
            await MarkdownEntraReportGenerator.WriteAsync(report, path, cancellationToken);
            _logger.LogInformation("Markdown report written to {Path}", path);
        }
    }

    private static EntraMonitorReport EmptyReport(int days) => new()
    {
        AuditReport = new EntraAuditReport
        {
            DaysReviewed = days,
            PeriodStart = DateTime.UtcNow.AddDays(-days),
            PeriodEnd = DateTime.UtcNow
        }
    };

    private async Task SendNotificationsAsync(EntraMonitorReport report, CancellationToken cancellationToken)
    {
        if (!report.HasChanges) return;

        try
        {
            var senders = NotificationFactory.Create(_config.Notifications, _loggerFactory);
            if (senders.Count == 0) return;

            var changeReport = report.ToChangeReport();
            var service = new NotificationService(senders, _loggerFactory.CreateLogger<NotificationService>());
            await service.NotifyAsync(changeReport, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Entra monitor notifications");
        }
    }
}
