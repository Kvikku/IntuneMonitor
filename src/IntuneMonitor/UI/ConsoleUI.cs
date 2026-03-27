using IntuneMonitor.Models;
using Spectre.Console;

namespace IntuneMonitor.UI;

/// <summary>
/// Rich terminal UI helpers using Spectre.Console.
/// </summary>
public static class ConsoleUI
{
    /// <summary>
    /// Displays the application banner.
    /// </summary>
    public static void WriteBanner()
    {
        AnsiConsole.Write(
            new FigletText("IntuneMonitor")
                .Color(Color.DodgerBlue1));
        AnsiConsole.MarkupLine("[dim]Export, import & monitor Intune policies[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a styled section header.
    /// </summary>
    public static void WriteHeader(string title)
    {
        AnsiConsole.Write(new Rule($"[bold dodgerblue1]{title}[/]").LeftJustified());
    }

    /// <summary>
    /// Renders the list-types table.
    /// </summary>
    public static void WriteContentTypesTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Supported Content Types[/]")
            .AddColumn(new TableColumn("[bold]Content Type[/]").PadRight(4))
            .AddColumn(new TableColumn("[bold]Backup File[/]"))
            .AddColumn(new TableColumn("[bold]Graph Endpoint[/]"));

        foreach (var ct in IntuneContentTypes.All)
        {
            table.AddRow(
                $"[cyan]{Markup.Escape(ct)}[/]",
                $"[dim]{Markup.Escape(IntuneContentTypes.FileNames[ct])}[/]",
                $"[dim]{Markup.Escape(IntuneContentTypes.GraphEndpoints[ct])}[/]");
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Renders an export summary table.
    /// </summary>
    public static void WriteExportSummary(int totalItems, IReadOnlyList<(string ContentType, int Count)> typeCounts)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]Export Summary[/]").LeftJustified());

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Content Type[/]").PadRight(4))
            .AddColumn(new TableColumn("[bold]Items[/]").RightAligned());

        foreach (var (contentType, count) in typeCounts)
        {
            var countStyle = count > 0 ? $"[green]{count}[/]" : "[dim]0[/]";
            table.AddRow($"[cyan]{Markup.Escape(contentType)}[/]", countStyle);
        }

        table.AddEmptyRow();
        table.AddRow("[bold]Total[/]", $"[bold green]{totalItems}[/]");

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Renders an import summary.
    /// </summary>
    public static void WriteImportSummary(int succeeded, int failed)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]Import Summary[/]").LeftJustified());

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Result[/]"))
            .AddColumn(new TableColumn("[bold]Count[/]").RightAligned());

        table.AddRow("[green]Succeeded[/]", $"[green]{succeeded}[/]");

        var failedStyle = failed > 0 ? $"[red]{failed}[/]" : "[dim]0[/]";
        table.AddRow("[red]Failed[/]", failedStyle);

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Renders the monitor change report as a styled table.
    /// </summary>
    public static void WriteChangeReport(ChangeReport report)
    {
        if (!report.HasChanges)
        {
            AnsiConsole.MarkupLine("[green]✓ No changes detected[/]");
            return;
        }

        // Summary bar
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Change Report[/]").LeftJustified());

        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Metric[/]")
            .AddColumn(new TableColumn("[bold]Count[/]").RightAligned());

        summaryTable.AddRow("[green]Added[/]", $"[green]{report.AddedCount}[/]");
        summaryTable.AddRow("[yellow]Modified[/]", $"[yellow]{report.ModifiedCount}[/]");
        summaryTable.AddRow("[red]Removed[/]", $"[red]{report.RemovedCount}[/]");
        summaryTable.AddRow("[bold]Total[/]", $"[bold]{report.TotalCount}[/]");

        AnsiConsole.Write(summaryTable);

        // Detail table grouped by content type
        var grouped = report.Changes
            .GroupBy(c => c.ContentType)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(group.Key)}[/]");

            var detail = new Table()
                .Border(TableBorder.Simple)
                .AddColumn(new TableColumn("[bold]Change[/]").Width(10))
                .AddColumn(new TableColumn("[bold]Severity[/]").Width(10))
                .AddColumn("[bold]Policy[/]")
                .AddColumn("[bold]Details[/]");

            foreach (var change in group)
            {
                var (changeIcon, changeColor) = change.ChangeType switch
                {
                    ChangeType.Added => ("✚ Added", "green"),
                    ChangeType.Removed => ("✖ Removed", "red"),
                    ChangeType.Modified => ("✎ Modified", "yellow"),
                    _ => ("?", "dim")
                };

                var severityColor = change.Severity switch
                {
                    ChangeSeverity.Critical => "red",
                    ChangeSeverity.Warning => "yellow",
                    _ => "blue"
                };

                var details = change.FieldChanges.Count > 0
                    ? $"{change.FieldChanges.Count} field change(s)"
                    : change.Details ?? "";

                detail.AddRow(
                    $"[{changeColor}]{changeIcon}[/]",
                    $"[{severityColor}]{change.Severity}[/]",
                    Markup.Escape(change.PolicyName),
                    $"[dim]{Markup.Escape(details)}[/]");
            }

            AnsiConsole.Write(detail);
        }
    }

    /// <summary>
    /// Wraps an async operation with a spinner status indicator.
    /// </summary>
    public static async Task<T> StatusAsync<T>(string message, Func<Task<T>> action)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("dodgerblue1"))
            .StartAsync(message, async _ => await action());
    }

    /// <summary>
    /// Wraps an async operation with a spinner status indicator.
    /// </summary>
    public static async Task StatusAsync(string message, Func<Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("dodgerblue1"))
            .StartAsync(message, async _ => await action());
    }

    /// <summary>
    /// Displays an info message.
    /// </summary>
    public static void Info(string message) =>
        AnsiConsole.MarkupLine($"[dodgerblue1]ℹ[/] {Markup.Escape(message)}");

    /// <summary>
    /// Displays a success message.
    /// </summary>
    public static void Success(string message) =>
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");

    /// <summary>
    /// Displays a warning message.
    /// </summary>
    public static void Warning(string message) =>
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");

    /// <summary>
    /// Displays an error message.
    /// </summary>
    public static void Error(string message) =>
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
}
