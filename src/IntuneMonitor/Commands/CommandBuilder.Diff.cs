using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
}
