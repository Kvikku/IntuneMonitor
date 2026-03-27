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

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>Intune Monitor – Change Report {Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm"))}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(HtmlTheme.GetStyles());
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body class=\"dark\">");

        // Header with theme toggle
        sb.AppendLine("<header>");
        sb.AppendLine("<div class=\"header-row\">");
        sb.AppendLine("<div>");
        sb.AppendLine("<h1>Intune Monitor – Change Report</h1>");
        sb.AppendLine($"<p class=\"meta\">Generated {Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC &middot; Tenant: <strong>{Encode(report.TenantName)}</strong></p>");
        sb.AppendLine("</div>");
        sb.AppendLine("<button id=\"theme-toggle\" onclick=\"toggleTheme()\" title=\"Toggle light/dark mode\">&#9788;</button>");
        sb.AppendLine("</div>");
        sb.AppendLine("</header>");

        // Summary cards
        sb.AppendLine("<section class=\"summary\">");
        AppendCard(sb, "Total Changes", report.TotalCount.ToString(), "total");
        AppendCard(sb, "Added", report.AddedCount.ToString(), "added");
        AppendCard(sb, "Modified", report.ModifiedCount.ToString(), "modified");
        AppendCard(sb, "Removed", report.RemovedCount.ToString(), "removed");
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
                sb.AppendLine($"<h2>{Encode(group.Key)} <span class=\"badge\">{group.Count()}</span></h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>Change</th><th>Severity</th><th>Policy Name</th><th>Policy ID</th><th>Details</th></tr></thead>");
                sb.AppendLine("<tbody>");

                foreach (var change in group.OrderBy(c => c.ChangeType).ThenBy(c => c.PolicyName))
                {
                    var changeClass = change.ChangeType.ToString().ToLowerInvariant();
                    var severityClass = change.Severity.ToString().ToLowerInvariant();

                    sb.AppendLine($"<tr class=\"{changeClass}\">");
                    sb.AppendLine($"<td><span class=\"change-tag {changeClass}\">{Encode(change.ChangeType.ToString())}</span></td>");
                    sb.AppendLine($"<td><span class=\"severity-tag {severityClass}\">{Encode(change.Severity.ToString())}</span></td>");
                    sb.AppendLine($"<td class=\"policy-name\">{Encode(change.PolicyName)}</td>");
                    sb.AppendLine($"<td class=\"policy-id\"><code>{Encode(change.PolicyId)}</code></td>");

                    // Details + field changes
                    sb.AppendLine("<td>");
                    if (!string.IsNullOrEmpty(change.Details))
                        sb.AppendLine($"<p class=\"detail-text\">{Encode(change.Details)}</p>");

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
                            sb.AppendLine($"<td class=\"field-path\">{Encode(field.FieldPath)}</td>");
                            sb.AppendLine($"<td class=\"old-value\">{Encode(Truncate(field.OldValue, 300))}</td>");
                            sb.AppendLine($"<td class=\"new-value\">{Encode(Truncate(field.NewValue, 300))}</td>");
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

        // Footer
        sb.AppendLine("<footer>Generated by IntuneMonitor</footer>");

        // Theme toggle script
        sb.AppendLine("<script>");
        sb.AppendLine(HtmlTheme.GetScript());
        sb.AppendLine("</script>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public static async Task WriteAsync(ChangeReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var html = Generate(report);
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, cancellationToken);
    }

    private static void AppendCard(StringBuilder sb, string label, string value, string cssClass)
    {
        sb.AppendLine($"<div class=\"card {cssClass}\">");
        sb.AppendLine($"<div class=\"card-value\">{Encode(value)}</div>");
        sb.AppendLine($"<div class=\"card-label\">{Encode(label)}</div>");
        sb.AppendLine("</div>");
    }

    private static string Encode(string? value) =>
        HttpUtility.HtmlEncode(value ?? "(null)");

    private static string Truncate(string? value, int maxLength) =>
        value == null ? "(null)" :
        value.Length <= maxLength ? value :
        value[..maxLength] + "…";
}
