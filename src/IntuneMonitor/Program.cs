using IntuneMonitor.Commands;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.CommandLine;

// ---------------------------------------------------------------------------
// Configuration loading
// ---------------------------------------------------------------------------
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "INTUNEMONITOR_");

var configuration = configBuilder.Build();
var appConfig = new AppConfiguration();
configuration.Bind(appConfig);

// ---------------------------------------------------------------------------
// CLI root command
// ---------------------------------------------------------------------------
var rootCommand = new RootCommand(
    "IntuneMonitor – Export/Import Intune policies and monitor for configuration changes.");

// Global options (can override appsettings.json)
var tenantIdOption = new Option<string?>(
    "--tenant-id", "Microsoft Entra tenant ID (overrides appsettings.json)");
var clientIdOption = new Option<string?>(
    "--client-id", "Application client ID (overrides appsettings.json)");
var clientSecretOption = new Option<string?>(
    "--client-secret", "Client secret (overrides appsettings.json)");
var certPathOption = new Option<string?>(
    "--cert-path", "Path to PFX/PEM certificate file (overrides appsettings.json)");
var certPasswordOption = new Option<string?>(
    "--cert-password", "Certificate password (overrides appsettings.json)");
var certThumbprintOption = new Option<string?>(
    "--cert-thumbprint", "Certificate thumbprint for cert-store lookup (overrides appsettings.json)");
var backupPathOption = new Option<string?>(
    "--backup-path", "Path to backup storage directory (overrides appsettings.json)");
var contentTypesOption = new Option<string[]>(
    "--content-types",
    () => Array.Empty<string>(),
    $"Content types to process. Available: {string.Join(", ", IntuneContentTypes.All)}")
    { AllowMultipleArgumentsPerToken = false };
var verbosityOption = new Option<LogLevel>(
    "--verbosity",
    () => LogLevel.Information,
    "Set the logging verbosity level (Trace, Debug, Information, Warning, Error, Critical, None)");

rootCommand.AddGlobalOption(tenantIdOption);
rootCommand.AddGlobalOption(clientIdOption);
rootCommand.AddGlobalOption(clientSecretOption);
rootCommand.AddGlobalOption(certPathOption);
rootCommand.AddGlobalOption(certPasswordOption);
rootCommand.AddGlobalOption(certThumbprintOption);
rootCommand.AddGlobalOption(backupPathOption);
rootCommand.AddGlobalOption(contentTypesOption);
rootCommand.AddGlobalOption(verbosityOption);

// ---------------------------------------------------------------------------
// export command
// ---------------------------------------------------------------------------
var exportCommand = new Command("export",
    "Export Intune policies to the configured backup storage (local file or Git).");
var exportHtmlReportOption = new Option<string?>(
    "--html-report", "Path to write an HTML export summary report (overrides appsettings.json).");
exportCommand.AddOption(exportHtmlReportOption);
rootCommand.AddCommand(exportCommand);

exportCommand.SetHandler(async (context) =>
{
    ApplyGlobalOverrides(appConfig, context.ParseResult,
        tenantIdOption, clientIdOption, clientSecretOption,
        certPathOption, certPasswordOption, certThumbprintOption, backupPathOption);

    var exportHtmlPath = context.ParseResult.GetValueForOption(exportHtmlReportOption);
    if (!string.IsNullOrWhiteSpace(exportHtmlPath))
        appConfig.Backup.HtmlExportReportPath = exportHtmlPath;

    var logLevel = context.ParseResult.GetValueForOption(verbosityOption);
    using var loggerFactory = CreateLoggerFactory(logLevel);

    var types = context.ParseResult.GetValueForOption(contentTypesOption);
    var cmd = new ExportCommand(appConfig, loggerFactory);
    await cmd.RunAsync(types?.Length > 0 ? types : null);
});

// ---------------------------------------------------------------------------
// import command
// ---------------------------------------------------------------------------
var importCommand = new Command("import",
    "Import Intune policies from the configured backup storage into the tenant.");
var dryRunOption = new Option<bool>(
    "--dry-run", "Preview changes without creating anything in the tenant.");
importCommand.AddOption(dryRunOption);
rootCommand.AddCommand(importCommand);

importCommand.SetHandler(async (context) =>
{
    ApplyGlobalOverrides(appConfig, context.ParseResult,
        tenantIdOption, clientIdOption, clientSecretOption,
        certPathOption, certPasswordOption, certThumbprintOption, backupPathOption);

    var logLevel = context.ParseResult.GetValueForOption(verbosityOption);
    using var loggerFactory = CreateLoggerFactory(logLevel);

    var types = context.ParseResult.GetValueForOption(contentTypesOption);
    var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
    var cmd = new ImportCommand(appConfig, loggerFactory);
    await cmd.RunAsync(types?.Length > 0 ? types : null, dryRun);
});

// ---------------------------------------------------------------------------
// monitor command
// ---------------------------------------------------------------------------
var monitorCommand = new Command("monitor",
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
monitorCommand.AddOption(reportPathOption);
monitorCommand.AddOption(htmlReportOption);
monitorCommand.AddOption(intervalOption);
monitorCommand.AddOption(changesOnlyOption);
rootCommand.AddCommand(monitorCommand);

monitorCommand.SetHandler(async (context) =>
{
    ApplyGlobalOverrides(appConfig, context.ParseResult,
        tenantIdOption, clientIdOption, clientSecretOption,
        certPathOption, certPasswordOption, certThumbprintOption, backupPathOption);

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

    var logLevel = context.ParseResult.GetValueForOption(verbosityOption);
    using var loggerFactory = CreateLoggerFactory(logLevel);

    var types = context.ParseResult.GetValueForOption(contentTypesOption);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var cmd = new MonitorCommand(appConfig, loggerFactory);
    await cmd.RunScheduledAsync(types?.Length > 0 ? types : null, cts.Token);
});

// ---------------------------------------------------------------------------
// audit-log command
// ---------------------------------------------------------------------------
var auditLogCommand = new Command("audit-log",
    "Review Intune audit logs and summarize changes for the last N days (1–30).");
var daysOption = new Option<int>(
    "--days", () => 7, "Number of days to review (1–30).");
var auditHtmlReportOption = new Option<string?>(
    "--html-report", "Path to write an HTML audit log report.");
var auditJsonReportOption = new Option<string?>(
    "--json-report", "Path to write a JSON audit log report.");
auditLogCommand.AddOption(daysOption);
auditLogCommand.AddOption(auditHtmlReportOption);
auditLogCommand.AddOption(auditJsonReportOption);
rootCommand.AddCommand(auditLogCommand);

auditLogCommand.SetHandler(async (context) =>
{
    ApplyGlobalOverrides(appConfig, context.ParseResult,
        tenantIdOption, clientIdOption, clientSecretOption,
        certPathOption, certPasswordOption, certThumbprintOption, backupPathOption);

    var logLevel = context.ParseResult.GetValueForOption(verbosityOption);
    using var loggerFactory = CreateLoggerFactory(logLevel);

    var days = context.ParseResult.GetValueForOption(daysOption);
    if (days < 1 || days > 30)
    {
        var logger = loggerFactory.CreateLogger("IntuneMonitor");
        logger.LogError("--days must be between 1 and 30 (got {Days})", days);
        context.ExitCode = 1;
        return;
    }

    var htmlPath = context.ParseResult.GetValueForOption(auditHtmlReportOption);
    var jsonPath = context.ParseResult.GetValueForOption(auditJsonReportOption);

    var cmd = new AuditLogCommand(appConfig, loggerFactory);
    await cmd.RunAsync(days, htmlPath, jsonPath);
});

// ---------------------------------------------------------------------------
// list-types command – utility to display supported content types
// ---------------------------------------------------------------------------
var listTypesCommand = new Command("list-types", "Display all supported Intune content types.");
rootCommand.AddCommand(listTypesCommand);
listTypesCommand.SetHandler((context) =>
{
    var logLevel = context.ParseResult.GetValueForOption(verbosityOption);
    using var loggerFactory = CreateLoggerFactory(logLevel);
    var logger = loggerFactory.CreateLogger("IntuneMonitor");

    logger.LogInformation("Supported content types:");
    foreach (var ct in IntuneContentTypes.All)
        logger.LogInformation("  {ContentType} → {FileName}", ct.PadRight(40), IntuneContentTypes.FileNames[ct]);
});

// ---------------------------------------------------------------------------
// Run
// ---------------------------------------------------------------------------
return await rootCommand.InvokeAsync(args);

// ---------------------------------------------------------------------------
// Local helpers
// ---------------------------------------------------------------------------
static ILoggerFactory CreateLoggerFactory(LogLevel minLevel)
{
    return LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(minLevel)
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });
    });
}

static void ApplyGlobalOverrides(
    AppConfiguration config,
    System.CommandLine.Parsing.ParseResult result,
    Option<string?> tenantIdOpt,
    Option<string?> clientIdOpt,
    Option<string?> clientSecretOpt,
    Option<string?> certPathOpt,
    Option<string?> certPasswordOpt,
    Option<string?> certThumbprintOpt,
    Option<string?> backupPathOpt)
{
    var tenantId = result.GetValueForOption(tenantIdOpt);
    var clientId = result.GetValueForOption(clientIdOpt);
    var clientSecret = result.GetValueForOption(clientSecretOpt);
    var certPath = result.GetValueForOption(certPathOpt);
    var certPassword = result.GetValueForOption(certPasswordOpt);
    var certThumbprint = result.GetValueForOption(certThumbprintOpt);
    var backupPath = result.GetValueForOption(backupPathOpt);

    if (!string.IsNullOrWhiteSpace(tenantId)) config.Authentication.TenantId = tenantId;
    if (!string.IsNullOrWhiteSpace(clientId)) config.Authentication.ClientId = clientId;
    if (!string.IsNullOrWhiteSpace(clientSecret))
    {
        config.Authentication.Method = "ClientSecret";
        config.Authentication.ClientSecret = clientSecret;
    }
    if (!string.IsNullOrWhiteSpace(certPath))
    {
        config.Authentication.Method = "Certificate";
        config.Authentication.CertificatePath = certPath;
    }
    if (!string.IsNullOrWhiteSpace(certPassword)) config.Authentication.CertificatePassword = certPassword;
    if (!string.IsNullOrWhiteSpace(certThumbprint))
    {
        config.Authentication.Method = "Certificate";
        config.Authentication.CertificateThumbprint = certThumbprint;
    }
    if (!string.IsNullOrWhiteSpace(backupPath)) config.Backup.Path = backupPath;
}
