namespace IntuneMonitor.Reporting;

internal static class ReportPath
{
    /// <summary>
    /// Inserts a timestamp folder between the directory and file name of a report path.
    /// Example: "reports/export-report.html" → "reports/2026-03-27_093838/export-report.html"
    /// </summary>
    internal static string WithTimestamp(string path, string timestamp)
    {
        var dir = Path.GetDirectoryName(path) ?? ".";
        var fileName = Path.GetFileName(path);
        return Path.Combine(dir, timestamp, fileName);
    }
}
