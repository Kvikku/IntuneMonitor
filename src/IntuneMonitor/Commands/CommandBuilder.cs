using IntuneMonitor.Config;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace IntuneMonitor.Commands;

/// <summary>
/// Builds the System.CommandLine <see cref="RootCommand"/> with all sub-commands,
/// keeping the entry point (Program.cs) slim and scannable.
/// </summary>
internal static class CommandBuilder
{
    /// <summary>
    /// Constructs and configures the root command with all sub-commands and global options.
    /// </summary>
    public static (RootCommand Command, GlobalOptions Options) Build(
        AppConfiguration appConfig,
        IHttpClientFactory httpClientFactory)
    {
        var rootCommand = new RootCommand(
            "IntuneMonitor – Export/Import Intune policies and monitor for configuration changes.");

        var options = new GlobalOptions();
        options.AddToCommand(rootCommand);

        RegisterExportCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterImportCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterMonitorCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterAuditLogCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterListTypesCommand(rootCommand);
        RegisterDiffCommand(rootCommand, options, appConfig);
        RegisterRollbackCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterDependencyCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterValidateCommand(rootCommand, options, appConfig);

        return (rootCommand, options);
    }

    private static void RegisterExportCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("export",
            "Export Intune policies to the configured backup storage (local file or Git).");
        var htmlReportOption = new Option<string?>(
            "--html-report", "Path to write an HTML export summary report (overrides appsettings.json).");
        command.AddOption(htmlReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var exportHtmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            if (!string.IsNullOrWhiteSpace(exportHtmlPath))
                appConfig.Backup.HtmlExportReportPath = exportHtmlPath;

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);
            var cmd = new ExportCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(types?.Length > 0 ? types : null);
        });
    }

    private static void RegisterImportCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("import",
            "Import Intune policies from the configured backup storage into the tenant.");
        var dryRunOption = new Option<bool>(
            "--dry-run", "Preview changes without creating anything in the tenant.");
        command.AddOption(dryRunOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var cmd = new ImportCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(types?.Length > 0 ? types : null, dryRun);
        });
    }

    private static void RegisterMonitorCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("monitor",
            "Compare current Intune state against the backup and report changes. " +
            "Uses Monitor.IntervalMinutes from appsettings.json for scheduled runs (0 = run once).");
        var reportPathOption = new Option<string?>(
            "--report-path", "Path to write the JSON change report (overrides appsettings.json).");
        var htmlReportOption = new Option<string?>(
            "--html-report", "Path to write an HTML dashboard report (overrides appsettings.json).");
        var intervalOption = new Option<int?>(
            "--interval", "Polling interval in minutes. 0 = run once (overrides appsettings.json).");
        var changesOnlyOption = new Option<bool>(
            "--changes-only", "Only print output when changes are detected.");
        command.AddOption(reportPathOption);
        command.AddOption(htmlReportOption);
        command.AddOption(intervalOption);
        command.AddOption(changesOnlyOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var reportPath = context.ParseResult.GetValueForOption(reportPathOption);
            if (!string.IsNullOrWhiteSpace(reportPath))
                appConfig.Monitor.ReportOutputPath = reportPath;

            var htmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            if (!string.IsNullOrWhiteSpace(htmlPath))
                appConfig.Monitor.HtmlReportOutputPath = htmlPath;

            var interval = context.ParseResult.GetValueForOption(intervalOption);
            if (interval.HasValue)
                appConfig.Monitor.IntervalMinutes = interval.Value;

            if (context.ParseResult.GetValueForOption(changesOnlyOption))
                appConfig.Monitor.ChangesOnly = true;

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var cmd = new MonitorCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunScheduledAsync(types?.Length > 0 ? types : null, cts.Token);
        });
    }

    private static void RegisterAuditLogCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("audit-log",
            "Review Intune audit logs and summarize changes for the last N days (1–30).");
        var daysOption = new Option<int>(
            "--days", () => 7, "Number of days to review (1–30).");
        daysOption.AddValidator(result =>
        {
            var value = result.GetValueForOption(daysOption);
            if (value < 1 || value > 30)
                result.ErrorMessage = "--days must be between 1 and 30.";
        });
        var htmlReportOption = new Option<string?>(
            "--html-report", "Path to write an HTML audit log report.");
        var jsonReportOption = new Option<string?>(
            "--json-report", "Path to write a JSON audit log report.");
        command.AddOption(daysOption);
        command.AddOption(htmlReportOption);
        command.AddOption(jsonReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var days = context.ParseResult.GetValueForOption(daysOption);
            var htmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            var jsonPath = context.ParseResult.GetValueForOption(jsonReportOption);

            var cmd = new AuditLogCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(days, htmlPath, jsonPath);
        });
    }

    private static void RegisterListTypesCommand(RootCommand rootCommand)
    {
        var command = new Command("list-types", "Display all supported Intune content types.");
        rootCommand.AddCommand(command);
        command.SetHandler((_) => ConsoleUI.WriteContentTypesTable());
    }

    private static void RegisterDiffCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig)
    {
        var command = new Command("diff",
            "Compare two backup snapshots to detect differences (no Graph API access required).");
        var sourceOption = new Option<string>(
            "--source", "Path to the source (baseline) backup.")
        { IsRequired = true };
        var targetOption = new Option<string>(
            "--target", "Path to the target (current) backup.")
        { IsRequired = true };
        var htmlReportOption = new Option<string?>(
            "--html-report", "Path to write an HTML diff report.");
        var jsonReportOption = new Option<string?>(
            "--json-report", "Path to write a JSON diff report.");
        command.AddOption(sourceOption);
        command.AddOption(targetOption);
        command.AddOption(htmlReportOption);
        command.AddOption(jsonReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);
            var source = context.ParseResult.GetValueForOption(sourceOption)!;
            var target = context.ParseResult.GetValueForOption(targetOption)!;
            var htmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            var jsonPath = context.ParseResult.GetValueForOption(jsonReportOption);

            var cmd = new DiffCommand(appConfig, loggerFactory);
            await cmd.RunAsync(source, target, types?.Length > 0 ? types : null, htmlPath, jsonPath);
        });
    }

    private static void RegisterRollbackCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("rollback",
            "Detect configuration drift and revert modified/removed policies to their backed-up state.");
        var dryRunOption = new Option<bool>(
            "--dry-run", "Preview rollback actions without making changes.");
        command.AddOption(dryRunOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

            var cmd = new RollbackCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(types?.Length > 0 ? types : null, dryRun);
        });
    }

    private static void RegisterDependencyCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("dependency",
            "Analyze policy relationships and dependencies from backup data.");
        var jsonReportOption = new Option<string?>(
            "--json-report", "Path to write a JSON dependency report.");
        command.AddOption(jsonReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var types = context.ParseResult.GetValueForOption(options.ContentTypes);
            var jsonPath = context.ParseResult.GetValueForOption(jsonReportOption);

            var cmd = new DependencyCommand(appConfig, loggerFactory);
            await cmd.RunAsync(types?.Length > 0 ? types : null, jsonPath);
        });
    }

    private static void RegisterValidateCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig)
    {
        var command = new Command("validate",
            "Validate backup files for integrity and import readiness.");
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var storage = BackupStorageFactory.Create(appConfig.Backup, loggerFactory);
            var validator = new BackupValidator(
                loggerFactory.CreateLogger<BackupValidator>());

            var results = await validator.ValidateStorageAsync(storage);
            var allValid = true;

            foreach (var (contentType, result) in results)
            {
                if (result.IsValid)
                {
                    ConsoleUI.Success($"{contentType}: Valid ({result.Warnings.Count} warning(s))");
                }
                else
                {
                    ConsoleUI.Error($"{contentType}: Invalid — {result.Errors.Count} error(s)");
                    foreach (var error in result.Errors)
                        ConsoleUI.Error($"  {error}");
                    allValid = false;
                }

                foreach (var warning in result.Warnings)
                    ConsoleUI.Warning($"  {warning}");
            }

            if (allValid)
                ConsoleUI.Success("All backups are valid");
            else
                ConsoleUI.Error("Some backups have validation errors");
        });
    }
}
