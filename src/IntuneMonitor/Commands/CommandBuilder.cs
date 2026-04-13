using IntuneMonitor.Config;
using System.CommandLine;

namespace IntuneMonitor.Commands;

/// <summary>
/// Builds the System.CommandLine <see cref="RootCommand"/> with all sub-commands,
/// keeping the entry point (Program.cs) slim and scannable.
/// Each Register*Command method lives in a separate partial-class file under Commands/.
/// </summary>
internal static partial class CommandBuilder
{
    /// <summary>
    /// Constructs and configures the root command with all sub-commands and global options.
    /// </summary>
    public static (RootCommand Command, GlobalOptions Options) Build(
        AppConfiguration appConfig,
        IHttpClientFactory httpClientFactory)
    {
        var rootCommand = new RootCommand(
            "IntuneMonitor – Export/Import Intune policies and monitor for configuration changes.");

        var options = new GlobalOptions();
        options.AddToCommand(rootCommand);

        RegisterExportCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterImportCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterMonitorCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterAuditLogCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterListTypesCommand(rootCommand);
        RegisterDiffCommand(rootCommand, options, appConfig);
        RegisterRollbackCommand(rootCommand, options, appConfig, httpClientFactory);
        RegisterDependencyCommand(rootCommand, options, appConfig);
        RegisterValidateCommand(rootCommand, options, appConfig);

        return (rootCommand, options);
    }
}
