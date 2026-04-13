using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
}
