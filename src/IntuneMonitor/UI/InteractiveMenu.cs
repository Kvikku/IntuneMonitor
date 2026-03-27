using IntuneMonitor.Commands;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace IntuneMonitor.UI;

/// <summary>
/// Interactive menu-driven UI shown when the app is launched with no arguments.
/// </summary>
public class InteractiveMenu
{
    private readonly AppConfiguration _config;
    private readonly Func<LogLevel, ILoggerFactory> _loggerFactoryCreator;

    public InteractiveMenu(AppConfiguration config, Func<LogLevel, ILoggerFactory> loggerFactoryCreator)
    {
        _config = config;
        _loggerFactoryCreator = loggerFactoryCreator;
    }

    public async Task<int> RunAsync()
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold dodgerblue1]What would you like to do?[/]")
                    .HighlightStyle(Style.Parse("dodgerblue1"))
                    .AddChoices(
                        "Export policies",
                        "Import policies",
                        "Monitor for changes",
                        "Review audit logs",
                        "List content types",
                        "Settings overview",
                        "Exit"));

            switch (choice)
            {
                case "Export policies":
                    await RunExportAsync();
                    break;
                case "Import policies":
                    await RunImportAsync();
                    break;
                case "Monitor for changes":
                    await RunMonitorAsync();
                    break;
                case "Review audit logs":
                    await RunAuditLogAsync();
                    break;
                case "List content types":
                    ConsoleUI.WriteContentTypesTable();
                    break;
                case "Settings overview":
                    ShowSettings();
                    break;
                case "Exit":
                    AnsiConsole.MarkupLine("[dim]Goodbye![/]");
                    return 0;
            }
        }
    }

    private async Task RunExportAsync()
    {
        var types = PromptContentTypes();
        var htmlReport = AnsiConsole.Confirm("Generate HTML export report?", _config.Backup.HtmlExportReportPath != null);

        if (htmlReport)
        {
            var path = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue(_config.Backup.HtmlExportReportPath ?? "reports/export-report.html")
                    .AllowEmpty());
            if (!string.IsNullOrWhiteSpace(path))
                _config.Backup.HtmlExportReportPath = path;
        }

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new ExportCommand(_config, loggerFactory);
        await cmd.RunAsync(types);
    }

    private async Task RunImportAsync()
    {
        var types = PromptContentTypes();
        var dryRun = AnsiConsole.Confirm("Dry run (preview only, no changes)?", true);

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new ImportCommand(_config, loggerFactory);
        await cmd.RunAsync(types, dryRun);
    }

    private async Task RunMonitorAsync()
    {
        var types = PromptContentTypes();

        var htmlReport = AnsiConsole.Confirm("Generate HTML change report?", _config.Monitor.HtmlReportOutputPath != null);
        if (htmlReport)
        {
            var path = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue(_config.Monitor.HtmlReportOutputPath ?? "reports/change-report.html")
                    .AllowEmpty());
            if (!string.IsNullOrWhiteSpace(path))
                _config.Monitor.HtmlReportOutputPath = path;
        }

        var scheduled = AnsiConsole.Confirm("Run on a schedule (repeat)?", _config.Monitor.IntervalMinutes > 0);
        if (scheduled)
        {
            var interval = AnsiConsole.Prompt(
                new TextPrompt<int>("  Interval in minutes:")
                    .DefaultValue(_config.Monitor.IntervalMinutes > 0 ? _config.Monitor.IntervalMinutes : 5)
                    .Validate(v => v > 0 ? ValidationResult.Success() : ValidationResult.Error("Must be greater than 0")));
            _config.Monitor.IntervalMinutes = interval;
        }
        else
        {
            _config.Monitor.IntervalMinutes = 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        if (scheduled)
            AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop scheduled monitoring[/]");

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new MonitorCommand(_config, loggerFactory);
        await cmd.RunScheduledAsync(types, cts.Token);
    }

    private async Task RunAuditLogAsync()
    {
        var days = AnsiConsole.Prompt(
            new TextPrompt<int>("How many days to review?")
                .DefaultValue(7)
                .Validate(v => v is >= 1 and <= 30
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Must be between 1 and 30")));

        string? htmlPath = null;
        var htmlReport = AnsiConsole.Confirm("Generate HTML audit log report?", false);
        if (htmlReport)
        {
            htmlPath = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue("reports/audit-log-report.html")
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(htmlPath))
                htmlPath = null;
        }

        string? jsonPath = null;
        var jsonReport = AnsiConsole.Confirm("Generate JSON audit log report?", false);
        if (jsonReport)
        {
            jsonPath = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue("reports/audit-log-report.json")
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(jsonPath))
                jsonPath = null;
        }

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new AuditLogCommand(_config, loggerFactory);
        await cmd.RunAsync(days, htmlPath, jsonPath);
    }

    private List<string>? PromptContentTypes()
    {
        var filterChoice = AnsiConsole.Confirm("Limit to specific content types? (No = all types)", false);
        if (!filterChoice)
            return null;

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select content types to include:")
                .HighlightStyle(Style.Parse("dodgerblue1"))
                .InstructionsText("[dim](Press [dodgerblue1]<space>[/] to toggle, [dodgerblue1]<enter>[/] to confirm)[/]")
                .AddChoices(IntuneContentTypes.All));

        return selected.Count > 0 ? selected : null;
    }

    private void ShowSettings()
    {
        AnsiConsole.WriteLine();
        ConsoleUI.WriteHeader("Current Settings");

        var root = new Tree("[bold]Configuration[/]");

        // Auth
        var authNode = root.AddNode("[bold cyan]Authentication[/]");
        authNode.AddNode($"Tenant ID:  [yellow]{SafeMarkup(_config.Authentication.TenantId)}[/]");
        authNode.AddNode($"Client ID:  [yellow]{SafeMarkup(_config.Authentication.ClientId)}[/]");
        authNode.AddNode($"Method:     [yellow]{SafeMarkup(_config.Authentication.Method)}[/]");
        if (_config.Authentication.Method == "Certificate")
        {
            if (!string.IsNullOrEmpty(_config.Authentication.CertificateThumbprint))
                authNode.AddNode($"Thumbprint: [yellow]{SafeMarkup(_config.Authentication.CertificateThumbprint)}[/]");
            if (!string.IsNullOrEmpty(_config.Authentication.CertificatePath))
                authNode.AddNode($"Cert path:  [yellow]{SafeMarkup(_config.Authentication.CertificatePath)}[/]");
        }

        // Backup
        var backupNode = root.AddNode("[bold cyan]Backup[/]");
        backupNode.AddNode($"Storage:    [yellow]{SafeMarkup(_config.Backup.StorageType)}[/]");
        backupNode.AddNode($"Path:       [yellow]{SafeMarkup(_config.Backup.Path)}[/]");
        if (_config.Backup.StorageType.Equals("Git", StringComparison.OrdinalIgnoreCase))
        {
            backupNode.AddNode($"Git remote: [yellow]{SafeMarkup(_config.Backup.GitRemoteUrl)}[/]");
            backupNode.AddNode($"Git branch: [yellow]{SafeMarkup(_config.Backup.GitBranch)}[/]");
            backupNode.AddNode($"Auto-commit:[yellow]{_config.Backup.AutoCommit}[/]");
        }
        if (!string.IsNullOrEmpty(_config.Backup.HtmlExportReportPath))
            backupNode.AddNode($"HTML report: [yellow]{SafeMarkup(_config.Backup.HtmlExportReportPath)}[/]");

        // Monitor
        var monitorNode = root.AddNode("[bold cyan]Monitor[/]");
        monitorNode.AddNode($"Interval:    [yellow]{(_config.Monitor.IntervalMinutes > 0 ? $"{_config.Monitor.IntervalMinutes} min" : "Run once")}[/]");
        monitorNode.AddNode($"Min severity:[yellow]{SafeMarkup(_config.Monitor.MinSeverity)}[/]");
        monitorNode.AddNode($"Changes only:[yellow]{_config.Monitor.ChangesOnly}[/]");
        if (!string.IsNullOrEmpty(_config.Monitor.HtmlReportOutputPath))
            monitorNode.AddNode($"HTML report: [yellow]{SafeMarkup(_config.Monitor.HtmlReportOutputPath)}[/]");

        // Content types
        if (_config.ContentTypes.Count > 0)
        {
            var ctNode = root.AddNode("[bold cyan]Content Type Filter[/]");
            foreach (var ct in _config.ContentTypes)
                ctNode.AddNode($"[yellow]{SafeMarkup(ct)}[/]");
        }

        AnsiConsole.Write(root);
    }

    private static string SafeMarkup(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[dim](not set)[/]" : Markup.Escape(value);
}
