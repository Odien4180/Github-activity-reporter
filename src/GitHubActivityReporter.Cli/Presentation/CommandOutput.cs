using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Models;
using Spectre.Console;

namespace GitHubActivityReporter.Cli.Presentation;

public static class CommandOutput
{
    public static void PrintConfigurationError(ConfigurationLoadException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        AnsiConsole.MarkupLineInterpolated($"[red]✗[/] {exception.Message}");
        foreach (var error in exception.Errors)
        {
            AnsiConsole.MarkupLineInterpolated($"  [red]-[/] {error}");
        }
    }

    public static void PrintRunSummary(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Item");
        table.AddColumn("Value");

        table.AddRow("Period", $"{outcome.Period.Start:yyyy-MM-dd HH:mm}Z – {outcome.Period.End:yyyy-MM-dd HH:mm}Z");
        table.AddRow("Public repositories", outcome.Report.PublicActivities.Count.ToString());
        table.AddRow("Public events", outcome.Report.PublicTotals.TotalCount.ToString());
        table.AddRow("Private repositories", outcome.Report.PrivateMetrics.ActiveRepositoryCount.ToString());
        table.AddRow("Private events", outcome.Report.PrivateMetrics.TotalEventCount.ToString());
        table.AddRow("Rendered artifacts", outcome.Pipeline.RenderedReports.Sum(r => r.Artifacts.Count).ToString());
        table.AddRow("Privacy validation", outcome.Pipeline.Validation.IsValid ? "[green]passed[/]" : "[red]failed[/]");
        table.AddRow("Report hash", outcome.ReportHash);

        AnsiConsole.Write(table);

        foreach (var result in outcome.Pipeline.PublishResults)
        {
            var color = result.Outcome switch
            {
                PublishOutcome.Published => "green",
                PublishOutcome.Failed => "red",
                _ => "yellow"
            };

            AnsiConsole.MarkupLineInterpolated(
                $"[{color}]{result.PublisherId}[/]: {result.Outcome} {result.Message ?? string.Empty}");
        }
    }
}
