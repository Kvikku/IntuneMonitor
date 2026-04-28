using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
    private static void RegisterEntraMonitorCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig, IHttpClientFactory httpClientFactory)
    {
        var command = new Command("entra-monitor",
            "Monitor Entra app registrations and enterprise applications for owner and permission changes.");

        var daysOption = new Option<int?>(
            "--days", "Number of days of audit logs to include (1–30).");
        daysOption.AddValidator(result =>
        {
            var value = result.GetValueForOption(daysOption);
            if (value.HasValue && (value.Value < 1 || value.Value > 30))
                result.ErrorMessage = "--days must be between 1 and 30.";
        });

        var snapshotPathOption = new Option<string?>(
            "--snapshot-path", "Local folder path for storing Entra snapshots.");
        var htmlReportOption = new Option<string?>(
            "--html-report", "Path to write an HTML report.");
        var mdReportOption = new Option<string?>(
            "--md-report", "Path to write a Markdown report.");
        var jsonReportOption = new Option<string?>(
            "--json-report", "Path to write a JSON report.");

        command.AddOption(daysOption);
        command.AddOption(snapshotPathOption);
        command.AddOption(htmlReportOption);
        command.AddOption(mdReportOption);
        command.AddOption(jsonReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var daysOverride = context.ParseResult.GetValueForOption(daysOption);
            var snapshotPath = context.ParseResult.GetValueForOption(snapshotPathOption);
            var htmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            var mdPath = context.ParseResult.GetValueForOption(mdReportOption);
            var jsonPath = context.ParseResult.GetValueForOption(jsonReportOption);

            if (daysOverride.HasValue)
                appConfig.EntraMonitor.Days = daysOverride.Value;

            var cmd = new EntraMonitorCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(appConfig.EntraMonitor.Days, snapshotPath, htmlPath, jsonPath, mdPath);
        });
    }
}
