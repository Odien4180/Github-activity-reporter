using System.ComponentModel;
using GitHubActivityReporter.Cli.Presentation;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.State;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public sealed class RunCommandSettings : ReporterSettings
{
    [CommandOption("--dry-run")]
    [Description("Run the whole pipeline but let publishers skip every side effect.")]
    public bool DryRun { get; init; }

    [CommandOption("--commit")]
    [Description("Let the GitHub profile publisher create a git commit.")]
    public bool Commit { get; init; }

    [CommandOption("--push")]
    [Description("Push the created commit. Requires --commit.")]
    public bool Push { get; init; }

    [CommandOption("--profile-path <PATH>")]
    [Description("Working copy of the profile repository. Defaults to the working directory.")]
    public string? ProfileRepositoryPath { get; init; }
}

public sealed class RunCommand : AsyncCommand<RunCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunCommandSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();
        var registry = new InMemoryPrivateTermRegistry();
        var log = new MaskingReporterLog(new SpectreReporterLog(verbose: settings.Verbose), registry);

        try
        {
            var loaded = await new ReporterContextLoader()
                .LoadAsync(settings.ConfigPath, workingDirectory, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var collectorResult = await new CollectorFactory()
                .CreateAsync(loaded.Configuration, registry, log, cancellationToken)
                .ConfigureAwait(false);

            if (!collectorResult.Succeeded)
            {
                log.Error(collectorResult.Error ?? "Could not create the GitHub collector.");
                return 1;
            }

            var runner = new ReportRunner(
                collectorResult.Collector!,
                registry,
                new FileReporterStateStore(workingDirectory),
                SystemClock.Instance,
                log);

            var outcome = await runner
                .ExecuteAsync(new RunOptions
                {
                    Configuration = loaded.Configuration,
                    WorkingDirectory = workingDirectory,
                    Preview = false,
                    DryRun = settings.DryRun,
                    CommitProfileRepository = settings.Commit,
                    PushProfileRepository = settings.Push,
                    ProfileRepositoryPath = settings.ProfileRepositoryPath
                }, cancellationToken)
                .ConfigureAwait(false);

            CommandOutput.PrintRunSummary(outcome);
            return outcome.Succeeded ? 0 : 1;
        }
        catch (ConfigurationLoadException exception)
        {
            CommandOutput.PrintConfigurationError(exception);
            return 1;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]✗[/] {log.Sanitize(exception.Message)}");
            return 1;
        }
    }
}
