using IntuneMonitor.Commands;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public InteractiveMenu(AppConfiguration config, Func<LogLevel, ILoggerFactory> loggerFactoryCreator, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _loggerFactoryCreator = loggerFactoryCreator;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<int> RunAsync()
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(MenuConstants.MainMenuTitle)
                    .HighlightStyle(Style.Parse("dodgerblue1"))
                    .AddChoices(MenuConstants.MainMenuChoices));

            switch (choice)
            {
                case MenuConstants.ExportPolicies:
                    await RunExportAsync();
                    break;
                case MenuConstants.ImportPolicies:
                    await RunImportAsync();
                    break;
                case MenuConstants.MonitorForChanges:
                    await RunMonitorAsync();
                    break;
                case MenuConstants.RollbackDrift:
                    await RunRollbackAsync();
                    break;
                case MenuConstants.CompareBackups:
                    await RunDiffAsync();
                    break;
                case MenuConstants.AnalyzeDependencies:
                    await RunDependencyAsync();
                    break;
                case MenuConstants.ValidateBackups:
                    await RunValidateAsync();
                    break;
                case MenuConstants.ReviewAuditLogs:
                    await RunAuditLogAsync();
                    break;
                case MenuConstants.ListContentTypes:
                    ConsoleUI.WriteContentTypesTable();
                    break;
                case MenuConstants.SettingsOverview:
                    ShowSettings();
                    break;
                case MenuConstants.Exit:
                    AnsiConsole.MarkupLine(MenuConstants.GoodbyeMessage);
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
        var cmd = new ExportCommand(_config, _httpClientFactory, loggerFactory);
        await cmd.RunAsync(types);
    }

    private async Task RunImportAsync()
    {
        var types = PromptContentTypes();
        var dryRun = AnsiConsole.Confirm(MenuConstants.DryRunPrompt, true);

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new ImportCommand(_config, _httpClientFactory, loggerFactory);
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
            AnsiConsole.MarkupLine(MenuConstants.ScheduledMonitoringHint);

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new MonitorCommand(_config, _httpClientFactory, loggerFactory);
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
        var cmd = new AuditLogCommand(_config, _httpClientFactory, loggerFactory);
        await cmd.RunAsync(days, htmlPath, jsonPath);
    }

    private async Task RunRollbackAsync()
    {
        var types = PromptContentTypes();
        var dryRun = AnsiConsole.Confirm(MenuConstants.DryRunPrompt, true);

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new RollbackCommand(_config, _httpClientFactory, loggerFactory);
        await cmd.RunAsync(types, dryRun);
    }

    private async Task RunDiffAsync()
    {
        var sourcePath = AnsiConsole.Prompt(
            new TextPrompt<string>("Source backup path (baseline):")
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Path is required")));

        var targetPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Target backup path (current):")
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Path is required")));

        var types = PromptContentTypes();

        string? htmlPath = null;
        if (AnsiConsole.Confirm("Generate HTML diff report?", false))
        {
            htmlPath = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue("reports/diff-report.html")
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(htmlPath)) htmlPath = null;
        }

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new DiffCommand(_config, loggerFactory);
        await cmd.RunAsync(sourcePath, targetPath, types, htmlPath);
    }

    private async Task RunDependencyAsync()
    {
        var types = PromptContentTypes();

        string? jsonPath = null;
        if (AnsiConsole.Confirm("Generate JSON dependency report?", false))
        {
            jsonPath = AnsiConsole.Prompt(
                new TextPrompt<string>("  Report path:")
                    .DefaultValue("reports/dependency-report.json")
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(jsonPath)) jsonPath = null;
        }

        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var cmd = new DependencyCommand(_config, loggerFactory);
        await cmd.RunAsync(types, jsonPath);
    }

    private async Task RunValidateAsync()
    {
        using var loggerFactory = _loggerFactoryCreator(LogLevel.Information);
        var storage = BackupStorageFactory.Create(_config.Backup, loggerFactory);
        var validator = new BackupValidator(loggerFactory.CreateLogger<BackupValidator>());

        var results = await validator.ValidateStorageAsync(storage);

        foreach (var (contentType, result) in results)
        {
            if (result.IsValid)
                ConsoleUI.Success($"{contentType}: Valid ({result.Warnings.Count} warning(s))");
            else
            {
                ConsoleUI.Error($"{contentType}: Invalid — {result.Errors.Count} error(s)");
                foreach (var error in result.Errors)
                    ConsoleUI.Error($"  {error}");
            }

            foreach (var warning in result.Warnings)
                ConsoleUI.Warning($"  {warning}");
        }

        if (results.All(r => r.Value.IsValid))
            ConsoleUI.Success("All backups are valid");
    }

    private List<string>? PromptContentTypes()
    {
        var filterChoice = AnsiConsole.Confirm(MenuConstants.ContentTypeFilterPrompt, false);
        if (!filterChoice)
            return null;

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title(MenuConstants.ContentTypeSelectionTitle)
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

        // Notifications
        var notifNode = root.AddNode("[bold cyan]Notifications[/]");
        if (_config.Notifications.Teams != null && !string.IsNullOrWhiteSpace(_config.Notifications.Teams.WebhookUrl))
            notifNode.AddNode("[yellow]Teams webhook: configured[/]");
        if (_config.Notifications.Slack != null && !string.IsNullOrWhiteSpace(_config.Notifications.Slack.WebhookUrl))
            notifNode.AddNode("[yellow]Slack webhook: configured[/]");
        if (_config.Notifications.Email != null && !string.IsNullOrWhiteSpace(_config.Notifications.Email.SmtpServer))
            notifNode.AddNode($"[yellow]Email: {SafeMarkup(_config.Notifications.Email.SmtpServer)} → {_config.Notifications.Email.ToAddresses.Count} recipient(s)[/]");
        if (notifNode.Nodes.Count == 0)
            notifNode.AddNode("[dim](none configured)[/]");

        // Tenant profiles
        if (_config.TenantProfiles.Count > 0)
        {
            var profileNode = root.AddNode("[bold cyan]Tenant Profiles[/]");
            foreach (var (name, profile) in _config.TenantProfiles)
                profileNode.AddNode($"[yellow]{SafeMarkup(name)}[/]: {SafeMarkup(profile.DisplayName)} (Tenant: {SafeMarkup(profile.Authentication.TenantId)})");
        }

        AnsiConsole.Write(root);
    }

    private static string SafeMarkup(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[dim](not set)[/]" : Markup.Escape(value);
}
