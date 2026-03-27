using System.Text.Json;
using Azure.Core;
using IntuneMonitor.Authentication;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.Models;
using IntuneMonitor.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Commands;

/// <summary>
/// Fetches Intune audit logs and summarizes changes over the last N days.
/// </summary>
public class AuditLogCommand
{
    private static readonly JsonSerializerOptions ReportWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppConfiguration _config;
    private readonly ILogger<AuditLogCommand> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AuditLogCommand(AppConfiguration config, ILoggerFactory? loggerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<AuditLogCommand>();
    }

    /// <summary>
    /// Runs the audit log review for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days to look back (1–30).</param>
    /// <param name="htmlReportPath">Optional path to write an HTML report.</param>
    /// <param name="jsonReportPath">Optional path to write a JSON report.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated audit log report.</returns>
    public async Task<AuditLogReport> RunAsync(
        int days,
        string? htmlReportPath = null,
        string? jsonReportPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Intune Audit Log Review ===");
        _logger.LogInformation("Reviewing audit logs for the last {Days} day(s)", days);

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

        // Fetch audit events
        var fetcher = new AuditLogFetcher(credential, _loggerFactory);
        List<AuditEvent> events;
        try
        {
            events = await fetcher.FetchAuditEventsAsync(days, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch audit logs from Graph");
            return EmptyReport(days);
        }

        // Build report
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddDays(-days);

        var report = BuildReport(events, days, periodStart, periodEnd, _config.Authentication.TenantId);

        // Print summary to console
        PrintSummary(report);

        // Write reports
        await WriteJsonReportAsync(report, jsonReportPath, cancellationToken);
        await WriteHtmlReportAsync(report, htmlReportPath, cancellationToken);

        return report;
    }

    /// <summary>
    /// Builds the audit log report with aggregated statistics.
    /// Kept internal for testability.
    /// </summary>
    internal static AuditLogReport BuildReport(
        List<AuditEvent> events,
        int days,
        DateTime periodStart,
        DateTime periodEnd,
        string? tenantId = null)
    {
        var byActivityType = events
            .Where(e => !string.IsNullOrEmpty(e.ActivityType))
            .GroupBy(e => e.ActivityType)
            .ToDictionary(g => g.Key, g => g.Count());

        var byComponent = events
            .Where(e => !string.IsNullOrEmpty(e.ComponentName))
            .GroupBy(e => e.ComponentName)
            .ToDictionary(g => g.Key, g => g.Count());

        var byActor = events
            .Select(e => GetActorName(e.Actor))
            .Where(name => !string.IsNullOrEmpty(name))
            .GroupBy(name => name!)
            .ToDictionary(g => g.Key, g => g.Count());

        return new AuditLogReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = tenantId ?? string.Empty,
            DaysReviewed = days,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalEvents = events.Count,
            Events = events,
            EventsByActivityType = byActivityType,
            EventsByComponent = byComponent,
            EventsByActor = byActor
        };
    }

    private void PrintSummary(AuditLogReport report)
    {
        _logger.LogInformation("─────────────────────────────────────────────");
        _logger.LogInformation("Audit Log Summary: {PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}",
            report.PeriodStart, report.PeriodEnd);
        _logger.LogInformation("Total events: {TotalEvents}", report.TotalEvents);
        _logger.LogInformation("─────────────────────────────────────────────");

        if (report.TotalEvents == 0)
        {
            _logger.LogInformation("No audit events found in the specified period");
            return;
        }

        // Activity type breakdown
        _logger.LogInformation("");
        _logger.LogInformation("By Activity Type:");
        foreach (var (activityType, count) in report.EventsByActivityType.OrderByDescending(kv => kv.Value))
        {
            _logger.LogInformation("  {ActivityType}: {Count}", activityType.PadRight(30), count);
        }

        // Component breakdown
        _logger.LogInformation("");
        _logger.LogInformation("By Component:");
        foreach (var (component, count) in report.EventsByComponent.OrderByDescending(kv => kv.Value))
        {
            _logger.LogInformation("  {Component}: {Count}", component.PadRight(40), count);
        }

        // Top actors
        _logger.LogInformation("");
        _logger.LogInformation("By Actor (top 10):");
        foreach (var (actor, count) in report.EventsByActor.OrderByDescending(kv => kv.Value).Take(10))
        {
            _logger.LogInformation("  {Actor}: {Count}", actor.PadRight(50), count);
        }

        _logger.LogInformation("─────────────────────────────────────────────");
    }

    private async Task WriteJsonReportAsync(AuditLogReport report, string? outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(report, ReportWriteOptions);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            _logger.LogInformation("JSON report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write JSON report to '{OutputPath}'", outputPath);
        }
    }

    private async Task WriteHtmlReportAsync(AuditLogReport report, string? outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            await HtmlAuditReportGenerator.WriteAsync(report, outputPath, cancellationToken);
            _logger.LogInformation("HTML report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write HTML report to '{OutputPath}'", outputPath);
        }
    }

    private static string? GetActorName(AuditActor? actor)
    {
        if (actor == null)
            return null;

        if (!string.IsNullOrEmpty(actor.UserPrincipalName))
            return actor.UserPrincipalName;

        if (!string.IsNullOrEmpty(actor.ApplicationDisplayName))
            return actor.ApplicationDisplayName;

        return null;
    }

    private static AuditLogReport EmptyReport(int days) =>
        new()
        {
            GeneratedAt = DateTime.UtcNow,
            DaysReviewed = days,
            PeriodStart = DateTime.UtcNow.AddDays(-days),
            PeriodEnd = DateTime.UtcNow
        };
}
