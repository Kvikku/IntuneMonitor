using System.Text;
using System.Web;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a self-contained HTML dashboard from an Intune change report.
/// </summary>
public static class HtmlReportGenerator
{
    public static string Generate(ChangeReport report)
    {
        var sb = new StringBuilder();

        HtmlReportHelpers.AppendDocumentHead(sb,
            pageTitle: $"Intune Monitor – Change Report {report.GeneratedAt:yyyy-MM-dd HH:mm}",
            headerTitle: "Intune Monitor – Change Report",
            subtitle: $"Generated {HtmlReportHelpers.Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC &middot; Tenant: <strong>{HtmlReportHelpers.Encode(report.TenantName)}</strong>");

        // Summary cards
        sb.AppendLine("<section class=\"summary\">");
        HtmlReportHelpers.AppendCard(sb, "Total Changes", report.TotalCount.ToString(), "total");
        HtmlReportHelpers.AppendCard(sb, "Added", report.AddedCount.ToString(), "added");
        HtmlReportHelpers.AppendCard(sb, "Modified", report.ModifiedCount.ToString(), "modified");
        HtmlReportHelpers.AppendCard(sb, "Removed", report.RemovedCount.ToString(), "removed");
        sb.AppendLine("</section>");

        if (!report.HasChanges)
        {
            sb.AppendLine("<section class=\"no-changes\"><p>No changes detected.</p></section>");
        }
        else
        {
            // Group by content type
            var grouped = report.Changes
                .GroupBy(c => c.ContentType)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"<section class=\"content-type\">");
                sb.AppendLine($"<h2>{HtmlReportHelpers.Encode(group.Key)} <span class=\"badge\">{group.Count()}</span></h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>Change</th><th>Severity</th><th>Policy Name</th><th>Policy ID</th><th>Details</th></tr></thead>");
                sb.AppendLine("<tbody>");

                foreach (var change in group.OrderBy(c => c.ChangeType).ThenBy(c => c.PolicyName))
                {
                    var changeClass = change.ChangeType.ToString().ToLowerInvariant();
                    var severityClass = change.Severity.ToString().ToLowerInvariant();

                    sb.AppendLine($"<tr class=\"{changeClass}\">");
                    sb.AppendLine($"<td><span class=\"change-tag {changeClass}\">{HtmlReportHelpers.Encode(change.ChangeType.ToString())}</span></td>");
                    sb.AppendLine($"<td><span class=\"severity-tag {severityClass}\">{HtmlReportHelpers.Encode(change.Severity.ToString())}</span></td>");
                    sb.AppendLine($"<td class=\"policy-name\">{HtmlReportHelpers.Encode(change.PolicyName)}</td>");
                    sb.AppendLine($"<td class=\"policy-id\"><code>{HtmlReportHelpers.Encode(change.PolicyId)}</code></td>");

                    // Details + field changes
                    sb.AppendLine("<td>");
                    if (!string.IsNullOrEmpty(change.Details))
                        sb.AppendLine($"<p class=\"detail-text\">{HtmlReportHelpers.Encode(change.Details)}</p>");

                    if (change.FieldChanges.Count > 0)
                    {
                        sb.AppendLine("<details>");
                        sb.AppendLine($"<summary>{change.FieldChanges.Count} field change(s)</summary>");
                        sb.AppendLine("<table class=\"field-table\">");
                        sb.AppendLine("<thead><tr><th>Field</th><th>Before</th><th>After</th></tr></thead>");
                        sb.AppendLine("<tbody>");

                        foreach (var field in change.FieldChanges)
                        {
                            sb.AppendLine("<tr>");
                            sb.AppendLine($"<td class=\"field-path\">{HtmlReportHelpers.Encode(field.FieldPath)}</td>");
                            sb.AppendLine($"<td class=\"old-value\">{HtmlReportHelpers.Encode(HtmlReportHelpers.Truncate(field.OldValue, 300))}</td>");
                            sb.AppendLine($"<td class=\"new-value\">{HtmlReportHelpers.Encode(HtmlReportHelpers.Truncate(field.NewValue, 300))}</td>");
                            sb.AppendLine("</tr>");
                        }

                        sb.AppendLine("</tbody></table>");
                        sb.AppendLine("</details>");
                    }

                    sb.AppendLine("</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
                sb.AppendLine("</section>");
            }
        }

        HtmlReportHelpers.AppendDocumentFoot(sb);

        return sb.ToString();
    }

    public static async Task WriteAsync(ChangeReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var html = Generate(report);
        await HtmlReportHelpers.WriteHtmlAsync(html, outputPath, cancellationToken);
    }
}
