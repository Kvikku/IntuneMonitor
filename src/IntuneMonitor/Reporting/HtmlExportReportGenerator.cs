using System.Text;
using System.Web;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a self-contained HTML dashboard summarizing an Intune export.
/// </summary>
public static class HtmlExportReportGenerator
{
    public static string Generate(ExportReport report)
    {
        var sb = new StringBuilder();

        HtmlReportHelpers.AppendDocumentHead(sb,
            pageTitle: $"Intune Monitor – Export Summary {HtmlReportHelpers.Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm"))}",
            headerTitle: "Intune Monitor – Export Summary",
            subtitle: $"Exported {HtmlReportHelpers.Encode(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))} UTC &middot; Tenant: <strong>{HtmlReportHelpers.Encode(report.TenantName)}</strong>",
            subtitle2: $"Storage: {HtmlReportHelpers.Encode(report.StorageType)} &middot; Path: <code>{HtmlReportHelpers.Encode(report.BackupPath)}</code>",
            extraStyles: GetExportStyles());

        // Summary cards
        sb.AppendLine("<section class=\"summary\">");
        HtmlReportHelpers.AppendCard(sb, "Total Items", report.TotalItems.ToString(), "total");
        HtmlReportHelpers.AppendCard(sb, "Content Types", report.ContentTypeCount.ToString(), "types");
        sb.AppendLine("</section>");

        if (report.ContentSummaries.Count == 0)
        {
            sb.AppendLine("<section class=\"no-changes\"><p>No items exported.</p></section>");
        }
        else
        {
            // Bar chart overview
            var maxCount = report.ContentSummaries.Max(s => s.ItemCount);
            sb.AppendLine("<section class=\"content-type\">");
            sb.AppendLine("<h2>Items by Content Type</h2>");
            sb.AppendLine("<div class=\"bar-chart\">");
            foreach (var summary in report.ContentSummaries.OrderByDescending(s => s.ItemCount))
            {
                var pct = maxCount > 0 ? (int)((double)summary.ItemCount / maxCount * 100) : 0;
                sb.AppendLine("<div class=\"bar-row\">");
                sb.AppendLine($"<span class=\"bar-label\">{HtmlReportHelpers.Encode(summary.ContentType)}</span>");
                sb.AppendLine($"<div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:{pct}%\"></div></div>");
                sb.AppendLine($"<span class=\"bar-count\">{summary.ItemCount}</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
            sb.AppendLine("</section>");

            // Detail tables per content type
            foreach (var summary in report.ContentSummaries.OrderBy(s => s.ContentType))
            {
                sb.AppendLine("<section class=\"content-type\">");
                sb.AppendLine($"<h2>{HtmlReportHelpers.Encode(summary.ContentType)} <span class=\"badge\">{summary.ItemCount}</span></h2>");

                if (summary.ItemNames.Count > 0)
                {
                    sb.AppendLine("<details open>");
                    sb.AppendLine($"<summary>{summary.ItemCount} item(s)</summary>");
                    sb.AppendLine("<table>");
                    sb.AppendLine("<thead><tr><th>#</th><th>Policy Name</th></tr></thead>");
                    sb.AppendLine("<tbody>");

                    int idx = 0;
                    foreach (var name in summary.ItemNames.OrderBy(n => n))
                    {
                        idx++;
                        sb.AppendLine($"<tr><td class=\"row-num\">{idx}</td><td>{HtmlReportHelpers.Encode(name)}</td></tr>");
                    }

                    sb.AppendLine("</tbody></table>");
                    sb.AppendLine("</details>");
                }

                sb.AppendLine("</section>");
            }
        }

        HtmlReportHelpers.AppendDocumentFoot(sb);

        return sb.ToString();
    }

    public static async Task WriteAsync(ExportReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var html = Generate(report);
        await HtmlReportHelpers.WriteHtmlAsync(html, outputPath, cancellationToken);
    }

    private static string GetExportStyles() => """
        .card.types { border-left-color: #8e44ad; }
        .card.types .card-value { color: #8e44ad; }
        body.dark .card.types { border-left-color: #b070d0; }
        body.dark .card.types .card-value { color: #b070d0; }
        .bar-chart { display: flex; flex-direction: column; gap: 10px; padding: 4px 0; }
        .bar-row { display: flex; align-items: center; gap: 12px; }
        .bar-label { width: 220px; font-size: 0.85rem; text-align: right; flex-shrink: 0; color: var(--fg); }
        .bar-track {
            flex: 1; height: 22px; background: var(--th-bg); border-radius: 4px; overflow: hidden;
        }
        .bar-fill {
            height: 100%; background: linear-gradient(90deg, #4a6fa5, #5ab8f5);
            border-radius: 4px; min-width: 2px; transition: width 0.4s ease;
        }
        .bar-count { width: 40px; font-size: 0.85rem; font-weight: 600; color: var(--fg); }
        .row-num { color: var(--meta); width: 40px; text-align: center; }
    """;
}
