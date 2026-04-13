using System.Text;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a Markdown report from an Intune audit log report.
/// Suitable for viewing inline in GitLab artifact browsers.
/// </summary>
public static class MarkdownAuditReportGenerator
{
    public static string Generate(AuditLogReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Intune Audit Log Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated **{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}** UTC · " +
                       $"Period: **{report.PeriodStart:yyyy-MM-dd}** to **{report.PeriodEnd:yyyy-MM-dd}** ({report.DaysReviewed} day(s))");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("| Total Events | Activity Types | Components | Actors |");
        sb.AppendLine("|:---:|:---:|:---:|:---:|");
        sb.AppendLine($"| **{report.TotalEvents}** | {report.EventsByActivityType.Count} | {report.EventsByComponent.Count} | {report.EventsByActor.Count} |");
        sb.AppendLine();

        if (report.TotalEvents == 0)
        {
            sb.AppendLine("✅ **No audit events found in the specified period.**");
            return sb.ToString();
        }

        // Activity Type breakdown
        AppendBreakdown(sb, "By Activity Type", report.EventsByActivityType);

        // Component breakdown
        AppendBreakdown(sb, "By Component", report.EventsByComponent);

        // Actor breakdown
        AppendBreakdown(sb, "By Actor", report.EventsByActor);

        // Recent events table
        sb.AppendLine($"## Recent Events ({report.Events.Count})");
        sb.AppendLine();
        sb.AppendLine("| Date/Time | Activity | Type | Component | Actor | Resources | Result |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var evt in report.Events.Take(200))
        {
            var actorName = evt.Actor?.UserPrincipalName ?? evt.Actor?.ApplicationDisplayName ?? "(unknown)";
            var resources = string.Join(", ", evt.Resources.Select(r =>
                string.IsNullOrEmpty(r.DisplayName) ? r.ResourceType : $"{r.DisplayName} ({r.ResourceType})"));
            var resultIcon = evt.ActivityResult.Equals("Success", StringComparison.OrdinalIgnoreCase) ? "✅" : "❌";

            sb.AppendLine($"| {evt.ActivityDateTime:yyyy-MM-dd HH:mm:ss} | {EscapePipe(evt.Activity)} | {EscapePipe(evt.ActivityType)} | {EscapePipe(evt.ComponentName)} | {EscapePipe(actorName)} | {EscapePipe(Truncate(resources, 120))} | {resultIcon} {EscapePipe(evt.ActivityResult)} |");
        }

        if (report.Events.Count > 200)
        {
            sb.AppendLine();
            sb.AppendLine($"*Showing first 200 of {report.Events.Count} events.*");
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public static async Task WriteAsync(AuditLogReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var content = Generate(report);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
    }

    private static void AppendBreakdown(StringBuilder sb, string title, Dictionary<string, int> data)
    {
        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine("| Name | Count |");
        sb.AppendLine("|---|---:|");
        foreach (var (name, count) in data.OrderByDescending(kv => kv.Value))
        {
            sb.AppendLine($"| {EscapePipe(name)} | {count} |");
        }
        sb.AppendLine();
    }

    private static string EscapePipe(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|");

    private static string Truncate(string? value, int maxLength) =>
        value == null ? "(null)" :
        value.Length <= maxLength ? value :
        value[..maxLength] + "…";
}
