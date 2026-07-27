using System.ComponentModel;
using GitHubActivityReporter.Cli.Presentation;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.State;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public sealed class PreviewCommandSettings : ReporterSettings
{
    [CommandOption("-o|--output <PATH>")]
    [Description("Directory the preview files are written to. Defaults to artifacts/preview.")]
    public string? OutputDirectory { get; init; }
}

/// <summary>
/// Generates every configured output without publishing anything:
/// no commit, no push, no external message is ever sent in preview mode.
/// </summary>
public sealed class PreviewCommand : AsyncCommand<PreviewCommandSettings>
{
    public const string DefaultPreviewDirectory = "artifacts/preview";

    protected override async Task<int> ExecuteAsync(CommandContext context, PreviewCommandSettings settings, CancellationToken cancellationToken)
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
                    Preview = true
                }, cancellationToken)
                .ConfigureAwait(false);

            if (!outcome.Pipeline.Validation.IsValid)
            {
                log.Error("Privacy validation failed, the preview files were not written.");
                return 1;
            }

            var previewRoot = Path.Combine(
                workingDirectory,
                string.IsNullOrWhiteSpace(settings.OutputDirectory) ? DefaultPreviewDirectory : settings.OutputDirectory!);

            var written = new List<string>();
            foreach (var artifact in outcome.Pipeline.RenderedReports.SelectMany(r => r.Artifacts))
            {
                var target = Path.Combine(previewRoot, Path.GetFileName(artifact.RelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, artifact.Content, cancellationToken).ConfigureAwait(false);
                written.Add(target);
            }

            CommandOutput.PrintRunSummary(outcome);
            AnsiConsole.MarkupLineInterpolated($"[green]•[/] Preview written to {previewRoot}");
            foreach (var file in written)
            {
                AnsiConsole.MarkupLineInterpolated($"  [grey]-[/] {file}");
            }

            AnsiConsole.MarkupLine("[yellow]![/] Preview mode: nothing was published, committed or sent.");
            return 0;
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
