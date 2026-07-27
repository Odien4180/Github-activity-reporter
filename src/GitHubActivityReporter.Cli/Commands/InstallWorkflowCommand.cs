using System.ComponentModel;
using GitHubActivityReporter.Bootstrap.GitHubActions;
using GitHubActivityReporter.Bootstrap.Generators;
using GitHubActivityReporter.Cli.Presentation;
using GitHubActivityReporter.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public sealed class InstallWorkflowCommandSettings : ReporterSettings
{
    [CommandOption("-r|--repository-path <PATH>")]
    [Description("Repository the workflow is installed into. Defaults to the working directory.")]
    public string? RepositoryPath { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print the generated workflow without writing it.")]
    public bool DryRun { get; init; }
}

public sealed class InstallWorkflowCommand : AsyncCommand<InstallWorkflowCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, InstallWorkflowCommandSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();

        try
        {
            var loaded = await new ReporterContextLoader()
                .LoadAsync(settings.ConfigPath, workingDirectory, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var repositoryPath = string.IsNullOrWhiteSpace(settings.RepositoryPath)
                ? workingDirectory
                : Path.GetFullPath(settings.RepositoryPath!);

            var result = await new WorkflowInstaller()
                .InstallAsync(
                    loaded.Configuration,
                    repositoryPath,
                    new WorkflowOptions(),
                    settings.DryRun,
                    loaded.Path,
                    cancellationToken)
                .ConfigureAwait(false);

            if (settings.DryRun)
            {
                AnsiConsole.WriteLine(result.Content);
                AnsiConsole.MarkupLineInterpolated($"[yellow]![/] Dry run: {result.Path} was not written.");
                return 0;
            }

            if (result.Changed)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Workflow written to {result.Path}");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Workflow already up to date: {result.Path}");
            }

            AnsiConsole.MarkupLineInterpolated(
                $"[grey]schedule:[/] {CronExpressionGenerator.Describe(loaded.Configuration.Schedule)}");
            AnsiConsole.MarkupLineInterpolated(
                $"[grey]secret:[/] gh secret set {loaded.Configuration.GitHub.TokenSecretName}");

            return 0;
        }
        catch (ConfigurationLoadException exception)
        {
            CommandOutput.PrintConfigurationError(exception);
            return 1;
        }
        catch (IOException exception)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]x[/] {exception.Message}");
            return 1;
        }
    }
}
