using System.ComponentModel;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public class ReporterSettings : CommandSettings
{
    [CommandOption("-c|--config <PATH>")]
    [Description("Path to activity-reporter.yml. Defaults to the nearest configuration file.")]
    public string? ConfigPath { get; init; }

    [CommandOption("-w|--working-directory <PATH>")]
    [Description("Directory used as the root for outputs and state. Defaults to the current directory.")]
    public string? WorkingDirectory { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Print additional diagnostic messages.")]
    public bool Verbose { get; init; }

    public string ResolveWorkingDirectory()
        => Path.GetFullPath(string.IsNullOrWhiteSpace(WorkingDirectory) ? Directory.GetCurrentDirectory() : WorkingDirectory!);
}
