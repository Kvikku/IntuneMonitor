using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
    private static void RegisterDependencyCommand(
        RootCommand rootCommand, GlobalOptions options,
        AppConfiguration appConfig)
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
}
