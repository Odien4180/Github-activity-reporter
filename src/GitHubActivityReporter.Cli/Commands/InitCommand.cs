using System.ComponentModel;
using GitHubActivityReporter.Bootstrap.ConfigurationSetup;
using GitHubActivityReporter.Bootstrap.GitHubActions;
using GitHubActivityReporter.Bootstrap.Generators;
using GitHubActivityReporter.Cli.Prompts;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.GitHub.Authentication;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public sealed class InitCommandSettings : ReporterSettings
{
    [CommandOption("-u|--username <LOGIN>")]
    [Description("GitHub login to report on. Detected automatically when omitted.")]
    public string? UserName { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Accept all defaults and never prompt.")]
    public bool NonInteractive { get; init; }

    [CommandOption("--force")]
    [Description("Overwrite an existing configuration file.")]
    public bool Force { get; init; }

    [CommandOption("--no-workflow")]
    [Description("Do not generate the GitHub Actions workflow.")]
    public bool SkipWorkflow { get; init; }
}

/// <summary>Interactive first time setup.</summary>
public sealed class InitCommand : AsyncCommand<InitCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, InitCommandSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();
        var prompts = new InitPrompts(interactive: !settings.NonInteractive);
        var cli = new GitHubCliClient();

        AnsiConsole.Write(new Rule("[bold]GitHub Activity Reporter setup[/]").LeftJustified());
        AnsiConsole.MarkupLineInterpolated($"[grey].NET runtime:[/] {Environment.Version}");

        var cliInstalled = await cli.IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine(cliInstalled
            ? "[green]✓[/] GitHub CLI detected"
            : "[yellow]![/] GitHub CLI (gh) not found. Repository checks are skipped.");

        var authentication = await cli.CheckAsync(null, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine(authentication.IsAuthenticated
            ? $"[green]✓[/] GitHub authenticated ({Markup.Escape(authentication.Source ?? "unknown")})"
            : "[yellow]![/] No GitHub credential detected. Set the token before running 'run'.");

        var userName = settings.UserName
                       ?? authentication.UserName
                       ?? prompts.Ask("GitHub username", Environment.UserName);

        if (string.IsNullOrWhiteSpace(userName))
        {
            AnsiConsole.MarkupLine("[red]✗[/] A GitHub username is required.");
            return 1;
        }

        userName = userName.Trim();
        AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Using GitHub username: {userName}");

        if (cliInstalled)
        {
            var exists = await cli.RepositoryExistsAsync(userName, userName, cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Profile repository {userName}/{userName} found");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]![/] Profile repository {userName}/{userName} was not found");
                if (prompts.IsInteractive && prompts.Confirm("Create the public profile repository now?", false))
                {
                    var created = await cli
                        .CreateProfileRepositoryAsync(userName, userName, cancellationToken)
                        .ConfigureAwait(false);

                    AnsiConsole.MarkupLine(created
                        ? "[green]✓[/] Profile repository created"
                        : "[red]✗[/] The profile repository could not be created. Create it manually.");
                }
            }
        }

        var answers = prompts.Collect(userName);
        var scaffolder = new ConfigurationScaffolder();
        var configuration = scaffolder.Build(answers);

        var validationErrors = new ReporterContextLoader().Validate(configuration);
        if (validationErrors.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] The generated configuration is not valid:");
            foreach (var error in validationErrors)
            {
                AnsiConsole.MarkupLineInterpolated($"  [red]-[/] {error}");
            }

            return 1;
        }

        var configPath = settings.ConfigPath is not null
            ? Path.GetFullPath(settings.ConfigPath)
            : Path.Combine(workingDirectory, ReporterConfiguration.DefaultFileName);

        try
        {
            await scaffolder.WriteAsync(configuration, configPath, settings.Force).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]✗[/] {exception.Message}");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Configuration written to {configPath}");

        if (!settings.SkipWorkflow && prompts.Confirm("Generate the GitHub Actions workflow?"))
        {
            var install = await new WorkflowInstaller()
                .InstallAsync(configuration, workingDirectory)
                .ConfigureAwait(false);

            AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Workflow written to {install.Path}");
            AnsiConsole.MarkupLineInterpolated(
                $"[grey]schedule:[/] {CronExpressionGenerator.Describe(configuration.Schedule)}");
        }

        AnsiConsole.Write(new Rule("[bold]Next steps[/]").LeftJustified());
        AnsiConsole.MarkupLineInterpolated(
            $"1. Create a fine grained token and register it: gh secret set {configuration.GitHub.TokenSecretName}");
        AnsiConsole.MarkupLineInterpolated(
            $"2. Export it locally: export {configuration.GitHub.TokenSecretName}=<token>");
        AnsiConsole.MarkupLine("3. Generate a preview: github-activity-reporter preview");
        AnsiConsole.MarkupLine("4. Check the setup: github-activity-reporter doctor");
        AnsiConsole.MarkupLine("5. Run the pipeline: github-activity-reporter run");

        return 0;
    }
}
