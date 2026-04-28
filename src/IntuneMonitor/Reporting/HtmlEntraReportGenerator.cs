using System.Text;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a self-contained HTML report for Entra app monitoring
/// (snapshot diff + directory audit events).
/// </summary>
public static class HtmlEntraReportGenerator
{
    public static string Generate(EntraMonitorReport report)
    {
        var sb = new StringBuilder();

        HtmlReportHelpers.AppendDocumentHead(sb,
            pageTitle: $"Entra App Monitor Report – {report.GeneratedAt:yyyy-MM-dd HH:mm}",
            headerTitle: "Entra App Monitor Report",
            subtitle: $"Generated {HtmlReportHelpers.Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC &middot; " +
                      $"Tenant: <strong>{HtmlReportHelpers.Encode(report.TenantId)}</strong>");

        // Summary cards
        sb.AppendLine("<section class=\"summary\">");
        HtmlReportHelpers.AppendCard(sb, "Snapshot Changes", report.TotalSnapshotChanges.ToString(), report.TotalSnapshotChanges > 0 ? "removed" : "total");
        HtmlReportHelpers.AppendCard(sb, "Audit Events", report.AuditReport.TotalEvents.ToString(), report.AuditReport.TotalEvents > 0 ? "modified" : "total");
        sb.AppendLine("</section>");

        if (!report.HasChanges)
        {
            HtmlReportHelpers.AppendNoDataSection(sb, "No changes detected. App registrations and enterprise applications are unchanged.");
        }
        else
        {
            // Snapshot changes section
            if (report.SnapshotChanges.Count > 0)
            {
                sb.AppendLine("<section class=\"content-type\">");
                sb.AppendLine($"<h2>Snapshot Changes <span class=\"badge\">{report.SnapshotChanges.Count}</span></h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>Category</th><th>Application</th><th>Change Type</th><th>Details</th></tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var change in report.SnapshotChanges)
                {
                    var cssClass = change.ChangeType switch
                    {
                        "Added" => "added",
                        "Removed" => "removed",
                        "OwnerAdded" or "OwnerRemoved" => "removed",
                        "PermissionChanged" => "modified",
                        _ => ""
                    };
                    sb.AppendLine($"<tr class=\"{cssClass}\">");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(change.Category)}</td>");
                    sb.AppendLine($"<td class=\"policy-name\">{HtmlReportHelpers.Encode(change.AppName)}</td>");
                    sb.AppendLine($"<td><span class=\"badge {cssClass}\">{HtmlReportHelpers.Encode(change.ChangeType)}</span></td>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(change.Details)}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table>");
                sb.AppendLine("</section>");
            }

            // Audit log sections
            if (report.AuditReport.TotalEvents > 0)
            {
                if (report.AuditReport.EventsByActivity.Count > 0)
                    HtmlReportHelpers.AppendBreakdownTable(sb, "Audit Events by Activity", report.AuditReport.EventsByActivity);

                if (report.AuditReport.EventsByActor.Count > 0)
                    HtmlReportHelpers.AppendBreakdownTable(sb, "Audit Events by Actor", report.AuditReport.EventsByActor);

                if (report.AuditReport.EventsByTargetApp.Count > 0)
                    HtmlReportHelpers.AppendBreakdownTable(sb, "Audit Events by Target Application", report.AuditReport.EventsByTargetApp);

                // Detailed event table
                sb.AppendLine("<section class=\"content-type\">");
                sb.AppendLine($"<h2>Audit Event Details <span class=\"badge\">{report.AuditReport.TotalEvents}</span></h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>Time</th><th>Activity</th><th>Initiated By</th><th>Target</th><th>Result</th></tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var evt in report.AuditReport.Events)
                {
                    var actor = evt.InitiatedBy?.UserPrincipalName ?? evt.InitiatedBy?.AppDisplayName ?? "(unknown)";
                    var target = evt.TargetResources.Count > 0 ? evt.TargetResources[0].DisplayName : "(unknown)";
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(evt.ActivityDateTime)}</td>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(evt.ActivityDisplayName)}</td>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(actor)}</td>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(target)}</td>");
                    sb.AppendLine($"<td>{HtmlReportHelpers.Encode(evt.Result)}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table>");
                sb.AppendLine("</section>");
            }
        }

        sb.AppendLine(HtmlTheme.GetScript());
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static async Task WriteAsync(EntraMonitorReport report, string path, CancellationToken cancellationToken = default)
    {
        var html = Generate(report);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, html, cancellationToken);
    }
}
