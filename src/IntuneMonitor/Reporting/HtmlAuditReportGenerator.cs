using System.Text;
using System.Web;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a self-contained HTML report from an Intune audit log summary.
/// </summary>
public static class HtmlAuditReportGenerator
{
    public static string Generate(AuditLogReport report)
    {
        var sb = new StringBuilder();

        HtmlReportHelpers.AppendDocumentHead(sb,
            pageTitle: $"Intune Audit Log Report – {report.GeneratedAt:yyyy-MM-dd HH:mm}",
            headerTitle: "Intune Audit Log Report",
            subtitle: $"Generated {HtmlReportHelpers.Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC &middot; " +
                      $"Period: <strong>{HtmlReportHelpers.Encode(report.PeriodStart.ToString("yyyy-MM-dd"))}</strong> to <strong>{HtmlReportHelpers.Encode(report.PeriodEnd.ToString("yyyy-MM-dd"))}</strong> " +
                      $"({report.DaysReviewed} day(s))");

        // Summary cards
        sb.AppendLine("<section class=\"summary\">");
        HtmlReportHelpers.AppendCard(sb, "Total Events", report.TotalEvents.ToString(), "total");
        HtmlReportHelpers.AppendCard(sb, "Activity Types", report.EventsByActivityType.Count.ToString(), "modified");
        HtmlReportHelpers.AppendCard(sb, "Components", report.EventsByComponent.Count.ToString(), "added");
        HtmlReportHelpers.AppendCard(sb, "Actors", report.EventsByActor.Count.ToString(), "removed");
        sb.AppendLine("</section>");

        if (report.TotalEvents == 0)
        {
            HtmlReportHelpers.AppendNoDataSection(sb, "No audit events found in the specified period.");
        }
        else
        {
            // Activity Type breakdown
            AppendBreakdownSection(sb, "By Activity Type", report.EventsByActivityType);

            // Component breakdown
            AppendBreakdownSection(sb, "By Component", report.EventsByComponent);

            // Actor breakdown
            AppendBreakdownSection(sb, "By Actor", report.EventsByActor);

            // Recent events table
            sb.AppendLine("<section class=\"content-type\">");
            sb.AppendLine($"<h2>Recent Events <span class=\"badge\">{report.Events.Count}</span></h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Date/Time</th><th>Activity</th><th>Type</th><th>Component</th><th>Actor</th><th>Resources</th><th>Result</th></tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var evt in report.Events.Take(500))
            {
                var resultClass = evt.ActivityResult.Equals("Success", StringComparison.OrdinalIgnoreCase) ? "added" : "removed";
                var actorName = evt.Actor?.UserPrincipalName ?? evt.Actor?.ApplicationDisplayName ?? "(unknown)";
                var resources = string.Join(", ", evt.Resources.Select(r =>
                    string.IsNullOrEmpty(r.DisplayName) ? r.ResourceType : $"{r.DisplayName} ({r.ResourceType})"));

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{HtmlReportHelpers.Encode(evt.ActivityDateTime.ToString("yyyy-MM-dd HH:mm:ss"))}</td>");
                sb.AppendLine($"<td class=\"policy-name\">{HtmlReportHelpers.Encode(evt.Activity)}</td>");
                sb.AppendLine($"<td><span class=\"change-tag modified\">{HtmlReportHelpers.Encode(evt.ActivityType)}</span></td>");
                sb.AppendLine($"<td>{HtmlReportHelpers.Encode(evt.ComponentName)}</td>");
                sb.AppendLine($"<td>{HtmlReportHelpers.Encode(actorName)}</td>");
                sb.AppendLine($"<td class=\"detail-text\">{HtmlReportHelpers.Encode(HtmlReportHelpers.Truncate(resources, 200))}</td>");
                sb.AppendLine($"<td><span class=\"change-tag {resultClass}\">{HtmlReportHelpers.Encode(evt.ActivityResult)}</span></td>");
                sb.AppendLine("</tr>");
            }

            if (report.Events.Count > 500)
            {
                sb.AppendLine($"<tr><td colspan=\"7\" style=\"text-align:center; color: var(--meta);\">Showing first 500 of {report.Events.Count} events</td></tr>");
            }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</section>");
        }

        HtmlReportHelpers.AppendDocumentFoot(sb);

        return sb.ToString();
    }

    public static async Task WriteAsync(AuditLogReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        await HtmlReportHelpers.GenerateAndWriteAsync(() => Generate(report), outputPath, cancellationToken);
    }

    private static void AppendBreakdownSection(StringBuilder sb, string title, Dictionary<string, int> data)
    {
        HtmlReportHelpers.AppendBreakdownTable(sb, title, data);
    }
}
