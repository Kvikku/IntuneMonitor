using IntuneMonitor.Config;
using IntuneMonitor.Storage;
using IntuneMonitor.UI;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
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
