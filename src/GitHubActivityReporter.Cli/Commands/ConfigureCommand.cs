using GitHubActivityReporter.Cli.Presentation;
using GitHubActivityReporter.Cli.Prompts;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

/// <summary>Interactively updates an existing configuration file.</summary>
public sealed class ConfigureCommand : AsyncCommand<ReporterSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ReporterSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();
        var contextLoader = new ReporterContextLoader();

        try
        {
            var loaded = await contextLoader
                .LoadAsync(settings.ConfigPath, workingDirectory, validate: false, cancellationToken)
                .ConfigureAwait(false);

            var prompts = new InitPrompts();
            if (!prompts.IsInteractive)
            {
                AnsiConsole.MarkupLine("[yellow]![/] configure requires an interactive terminal.");
                return 1;
            }

            var configuration = loaded.Configuration;
            var answers = prompts.Collect(configuration.GitHub.Username);
            var updated = new GitHubActivityReporter.Bootstrap.ConfigurationSetup.ConfigurationScaffolder().Build(answers);

            // Preserve settings that are not part of the interactive questionnaire.
            updated.GitHub.TokenSecretName = configuration.GitHub.TokenSecretName;
            updated.GitHub.ProfileRepository.Branch = configuration.GitHub.ProfileRepository.Branch;
            updated.Publishers.Local.OutputDirectory = configuration.Publishers.Local.OutputDirectory;
            updated.Outputs.GitHubProfile.Target = configuration.Outputs.GitHubProfile.Target;
            updated.Outputs.Json.Target = configuration.Outputs.Json.Target;
            updated.Privacy.CustomForbiddenTerms = configuration.Privacy.CustomForbiddenTerms;

            var errors = contextLoader.Validate(updated);
            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]✗[/] The updated configuration is not valid:");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLineInterpolated($"  [red]-[/] {error}");
                }

                return 1;
            }

            await ConfigurationLoader.Default.SaveAsync(updated, loaded.Path, cancellationToken).ConfigureAwait(false);
            AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Configuration updated: {loaded.Path}");
            return 0;
        }
        catch (ConfigurationLoadException exception)
        {
            CommandOutput.PrintConfigurationError(exception);
            return 1;
        }
    }
}
