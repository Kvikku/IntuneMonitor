using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
}
