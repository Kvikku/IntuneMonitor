using IntuneMonitor.UI;
using System.CommandLine;

namespace IntuneMonitor.Commands;

internal static partial class CommandBuilder
{
    private static void RegisterListTypesCommand(RootCommand rootCommand)
    {
        var command = new Command("list-types", "Display all supported Intune content types.");
        rootCommand.AddCommand(command);
        command.SetHandler((context) => ConsoleUI.WriteContentTypesTable());
    }
}
