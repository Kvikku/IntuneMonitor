using System.Globalization;
using System.Text;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a Markdown report for Entra app monitoring
/// (snapshot diff + directory audit events).
/// </summary>
public static class MarkdownEntraReportGenerator
{
    public static string Generate(EntraMonitorReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Entra App Monitor Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Tenant:** {report.TenantId}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Metric | Count |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Snapshot Changes | {report.TotalSnapshotChanges} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Audit Events | {report.AuditReport.TotalEvents} |");
        sb.AppendLine();

        if (!report.HasChanges)
        {
            sb.AppendLine("*No changes detected. App registrations and enterprise applications are unchanged.*");
            return sb.ToString();
        }

        // Snapshot changes
        if (report.SnapshotChanges.Count > 0)
        {
            sb.AppendLine("## Snapshot Changes");
            sb.AppendLine();
            sb.AppendLine("| Category | Application | Change Type | Details |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var change in report.SnapshotChanges)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {change.Category} | {change.AppName} | {change.ChangeType} | {change.Details} |");
            }
            sb.AppendLine();
        }

        // Audit events by activity
        if (report.AuditReport.EventsByActivity.Count > 0)
        {
            sb.AppendLine("## Audit Events by Activity");
            sb.AppendLine();
            sb.AppendLine("| Activity | Count |");
            sb.AppendLine("| --- | --- |");
            foreach (var (activity, count) in report.AuditReport.EventsByActivity.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {activity} | {count} |");
            }
            sb.AppendLine();
        }

        // Audit events by actor
        if (report.AuditReport.EventsByActor.Count > 0)
        {
            sb.AppendLine("## Audit Events by Actor");
            sb.AppendLine();
            sb.AppendLine("| Actor | Count |");
            sb.AppendLine("| --- | --- |");
            foreach (var (actor, count) in report.AuditReport.EventsByActor.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {actor} | {count} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static async Task WriteAsync(EntraMonitorReport report, string path, CancellationToken cancellationToken = default)
    {
        var md = Generate(report);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, md, cancellationToken);
    }
}
