using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
        var mdReportOption = new Option<string?>(
            "--md-report", "Path to write a Markdown audit log report.");
        var jsonReportOption = new Option<string?>(
            "--json-report", "Path to write a JSON audit log report.");
        command.AddOption(daysOption);
        command.AddOption(htmlReportOption);
        command.AddOption(mdReportOption);
        command.AddOption(jsonReportOption);
        rootCommand.AddCommand(command);

        command.SetHandler(async (context) =>
        {
            CliHelpers.ApplyGlobalOverrides(appConfig, context.ParseResult, options);

            var logLevel = context.ParseResult.GetValueForOption(options.Verbosity);
            using var loggerFactory = CliHelpers.CreateLoggerFactory(logLevel);

            var days = context.ParseResult.GetValueForOption(daysOption);
            var htmlPath = context.ParseResult.GetValueForOption(htmlReportOption);
            var mdPath = context.ParseResult.GetValueForOption(mdReportOption);
            var jsonPath = context.ParseResult.GetValueForOption(jsonReportOption);

            var cmd = new AuditLogCommand(appConfig, httpClientFactory, loggerFactory);
            await cmd.RunAsync(days, htmlPath, jsonPath, mdPath);
        });
    }
}
