using System.Text;
using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates a Markdown report from an Intune change report.
/// Suitable for viewing inline in GitLab artifact browsers.
/// </summary>
public static class MarkdownReportGenerator
{
    public static string Generate(ChangeReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Intune Monitor – Change Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated **{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}** UTC · Tenant: **{report.TenantName}**");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("| Total Changes | Added | Modified | Removed |");
        sb.AppendLine("|:---:|:---:|:---:|:---:|");
        sb.AppendLine($"| **{report.TotalCount}** | {report.AddedCount} | {report.ModifiedCount} | {report.RemovedCount} |");
        sb.AppendLine();

        if (!report.HasChanges)
        {
            sb.AppendLine("✅ **No changes detected.**");
            return sb.ToString();
        }

        // Group by content type
        var grouped = report.Changes
            .GroupBy(c => c.ContentType)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key} ({group.Count()})");
            sb.AppendLine();
            sb.AppendLine("| Change | Severity | Policy Name | Policy ID |");
            sb.AppendLine("|:---:|:---:|---|---|");

            foreach (var change in group.OrderBy(c => c.ChangeType).ThenBy(c => c.PolicyName))
            {
                var icon = change.ChangeType switch
                {
                    ChangeType.Added => "🟢 Added",
                    ChangeType.Removed => "🔴 Removed",
                    ChangeType.Modified => "🟡 Modified",
                    _ => change.ChangeType.ToString()
                };

                var severity = change.Severity switch
                {
                    ChangeSeverity.Critical => "🔴 Critical",
                    ChangeSeverity.Warning => "🟡 Warning",
                    _ => "ℹ️ Info"
                };

                sb.AppendLine($"| {icon} | {severity} | {EscapePipe(change.PolicyName)} | `{change.PolicyId}` |");
            }

            sb.AppendLine();

            // Field-level details for modified policies
            var modified = group
                .Where(c => c.FieldChanges.Count > 0)
                .OrderBy(c => c.PolicyName)
                .ToList();

            foreach (var change in modified)
            {
                sb.AppendLine($"<details><summary>📋 {EscapePipe(change.PolicyName)} – {change.FieldChanges.Count} field change(s)</summary>");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(change.Details))
                {
                    sb.AppendLine($"> {change.Details}");
                    sb.AppendLine();
                }

                sb.AppendLine("| Field | Before | After |");
                sb.AppendLine("|---|---|---|");

                foreach (var field in change.FieldChanges)
                {
                    sb.AppendLine($"| `{EscapePipe(field.FieldPath)}` | {EscapePipe(Truncate(field.OldValue, 200))} | {EscapePipe(Truncate(field.NewValue, 200))} |");
                }

                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public static async Task WriteAsync(ChangeReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var content = Generate(report);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
    }

    private static string EscapePipe(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|");

    private static string Truncate(string? value, int maxLength) =>
        value == null ? "(null)" :
        value.Length <= maxLength ? value :
        value[..maxLength] + "…";
}
