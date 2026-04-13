using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
}
