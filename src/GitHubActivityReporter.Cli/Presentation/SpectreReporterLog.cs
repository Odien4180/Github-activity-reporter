using GitHubActivityReporter.Core.Abstractions;
using Spectre.Console;

namespace GitHubActivityReporter.Cli.Presentation;

/// <summary>Writes log lines to the terminal. Always wrapped in a masking log.</summary>
public sealed class SpectreReporterLog : IReporterLog
{
    private readonly IAnsiConsole _console;
    private readonly bool _verbose;

    public SpectreReporterLog(IAnsiConsole? console = null, bool verbose = false)
    {
        _console = console ?? AnsiConsole.Console;
        _verbose = verbose;
    }

    public void Debug(string message)
    {
        if (_verbose)
        {
            _console.MarkupLineInterpolated($"[grey]debug[/] {message}");
        }
    }

    public void Info(string message) => _console.MarkupLineInterpolated($"[green]•[/] {message}");

    public void Warning(string message) => _console.MarkupLineInterpolated($"[yellow]![/] {message}");

    public void Error(string message) => _console.MarkupLineInterpolated($"[red]✗[/] {message}");
}
