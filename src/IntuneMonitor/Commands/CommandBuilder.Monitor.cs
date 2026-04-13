using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
        var mdReportOption = new Option<string?>(
            "--md-report", "Path to write a Markdown report (viewable inline in GitLab).");
        var intervalOption = new Option<int?>(
            "--interval", "Polling interval in minutes. 0 = run once (overrides appsettings.json).");
        var changesOnlyOption = new Option<bool>(
            "--changes-only", "Only print output when changes are detected.");
        command.AddOption(reportPathOption);
        command.AddOption(htmlReportOption);
        command.AddOption(mdReportOption);
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

            var mdPath = context.ParseResult.GetValueForOption(mdReportOption);
            if (!string.IsNullOrWhiteSpace(mdPath))
                appConfig.Monitor.MdReportOutputPath = mdPath;

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
}
