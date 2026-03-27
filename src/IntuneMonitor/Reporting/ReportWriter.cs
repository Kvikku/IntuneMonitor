using System.Text.Json;
using IntuneMonitor.Graph;
using Microsoft.Extensions.Logging;

namespace IntuneMonitor.Reporting;

/// <summary>
/// Shared utilities for writing JSON reports and opening HTML reports in the browser.
/// Eliminates duplicated report-writing logic across command classes.
/// </summary>
internal static class ReportWriter
{
    /// <summary>
    /// Serializes <paramref name="report"/> as indented JSON and writes it to
    /// <paramref name="outputPath"/>, creating intermediate directories as needed.
    /// </summary>
    public static async Task WriteJsonAsync<T>(
        T report,
        string outputPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(report, JsonDefaults.IndentedCamelCase);
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            logger.LogInformation("Report written to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write report to '{OutputPath}'", outputPath);
        }
    }

    /// <summary>
    /// Opens the specified file in the default browser/application.
    /// Errors are silently caught (opening a browser is non-critical).
    /// </summary>
    public static void OpenInBrowser(string outputPath, ILogger logger)
    {
        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open report in browser: {OutputPath}", outputPath);
        }
    }
}
