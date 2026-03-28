using IntuneMonitor.Models;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Generates CSV reports for change reports, export summaries, and audit logs.
/// </summary>
public static class CsvReportGenerator
{
    /// <summary>
    /// Writes a change report to CSV format.
    /// </summary>
    public static async Task WriteChangeReportAsync(ChangeReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        await writer.WriteLineAsync("ContentType,PolicyId,PolicyName,ChangeType,Severity,DetectedAt,FieldPath,OldValue,NewValue");

        foreach (var change in report.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (change.FieldChanges.Count == 0)
            {
                await writer.WriteLineAsync(FormatRow(
                    change.ContentType, change.PolicyId, change.PolicyName,
                    change.ChangeType.ToString(), change.Severity.ToString(),
                    change.DetectedAt.ToString("o"), "", "", ""));
            }
            else
            {
                foreach (var field in change.FieldChanges)
                {
                    await writer.WriteLineAsync(FormatRow(
                        change.ContentType, change.PolicyId, change.PolicyName,
                        change.ChangeType.ToString(), change.Severity.ToString(),
                        change.DetectedAt.ToString("o"),
                        field.FieldPath, field.OldValue ?? "", field.NewValue ?? ""));
                }
            }
        }
    }

    /// <summary>
    /// Writes an export summary to CSV format.
    /// </summary>
    public static async Task WriteExportReportAsync(ExportReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        await writer.WriteLineAsync("ContentType,ItemCount,ItemName");

        foreach (var summary in report.ContentSummaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (summary.ItemNames.Count == 0)
            {
                await writer.WriteLineAsync(FormatRow(summary.ContentType, summary.ItemCount.ToString(), ""));
            }
            else
            {
                foreach (var name in summary.ItemNames)
                {
                    await writer.WriteLineAsync(FormatRow(summary.ContentType, summary.ItemCount.ToString(), name));
                }
            }
        }
    }

    /// <summary>
    /// Writes an audit log report to CSV format.
    /// </summary>
    public static async Task WriteAuditReportAsync(AuditLogReport report, string outputPath, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        await writer.WriteLineAsync("DateTime,Activity,ActivityType,ComponentName,Actor,Result,Resources");

        foreach (var evt in report.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var actor = evt.Actor?.UserPrincipalName ?? evt.Actor?.ApplicationDisplayName ?? "";
            var resources = string.Join("; ", evt.Resources.Select(r => r.DisplayName));

            await writer.WriteLineAsync(FormatRow(
                evt.ActivityDateTime.ToString("o"),
                evt.Activity, evt.ActivityType, evt.ComponentName,
                actor, evt.ActivityResult, resources));
        }
    }

    /// <summary>
    /// Formats CSV fields with proper escaping (RFC 4180).
    /// </summary>
    private static string FormatRow(params string?[] fields)
    {
        return string.Join(",", fields.Select(EscapeCsvField));
    }

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Quote if contains special CSV chars or starts with formula prefix characters
        // to prevent CSV injection (formula injection) in spreadsheet applications
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            || value.StartsWith(' ') || value.EndsWith(' ')
            || value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
