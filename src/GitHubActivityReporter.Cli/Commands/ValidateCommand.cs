using System.ComponentModel;
using GitHubActivityReporter.Cli.Presentation;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public sealed class ValidateCommandSettings : ReporterSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("File or directory to validate. Defaults to the configured output targets.")]
    public string? Path { get; init; }
}

/// <summary>Scans already generated files for private identifiers, secrets and tokens.</summary>
public sealed class ValidateCommand : AsyncCommand<ValidateCommandSettings>
{
    private static readonly string[] ValidatedExtensions =
        [".md", ".json", ".svg", ".html", ".htm", ".css", ".js", ".txt", ".yml", ".yaml"];

    protected override async Task<int> ExecuteAsync(CommandContext context, ValidateCommandSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();
        var registry = new InMemoryPrivateTermRegistry();

        try
        {
            var loaded = await new ReporterContextLoader()
                .LoadAsync(settings.ConfigPath, workingDirectory, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var files = ResolveFiles(settings.Path, workingDirectory, loaded).ToArray();
            if (files.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]![/] No generated file was found to validate.");
                return 0;
            }

            var validator = new PrivacyValidator();
            var validationContext = ValidationContext.Create(registry, loaded.Configuration);
            var issues = new List<ValidationIssue>();

            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var relative = System.IO.Path.GetRelativePath(workingDirectory, file);
                var result = validator.ValidateContent(content, relative, validationContext);
                issues.AddRange(result.Issues);

                if (result.IsValid)
                {
                    AnsiConsole.MarkupLineInterpolated($"[green]✓[/] {relative}");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]✗[/] {relative}");
                }
            }

            foreach (var issue in issues)
            {
                var color = issue.Severity == ValidationSeverity.Error ? "red" : "yellow";
                AnsiConsole.MarkupLineInterpolated($"[{color}]{issue.Severity}[/] {issue.RuleId}: {issue.Message} ({issue.Target})");
            }

            var failed = issues.Any(i => i.Severity == ValidationSeverity.Error);
            if (failed)
            {
                AnsiConsole.MarkupLine("[red]Privacy validation failed. Publishing must not happen.[/]");
                return 1;
            }

            AnsiConsole.MarkupLineInterpolated($"[green]•[/] Validated {files.Length} file(s).");
            return 0;
        }
        catch (ConfigurationLoadException exception)
        {
            CommandOutput.PrintConfigurationError(exception);
            return 1;
        }
    }

    private static IEnumerable<string> ResolveFiles(string? explicitPath, string workingDirectory, LoadedConfiguration loaded)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var full = System.IO.Path.GetFullPath(explicitPath!);
            if (File.Exists(full))
            {
                yield return full;
                yield break;
            }

            if (Directory.Exists(full))
            {
                foreach (var file in EnumerateFiles(full))
                {
                    yield return file;
                }
            }

            yield break;
        }

        var candidates = new List<string>();
        var configuration = loaded.Configuration;

        if (configuration.Outputs.GitHubProfile.Enabled)
        {
            candidates.Add(configuration.Outputs.GitHubProfile.Target);
        }

        if (configuration.Outputs.Json.Enabled)
        {
            candidates.Add(configuration.Outputs.Json.Target);
        }

        foreach (var candidate in candidates)
        {
            var direct = System.IO.Path.Combine(workingDirectory, candidate);
            if (File.Exists(direct))
            {
                yield return direct;
            }

            if (configuration.Publishers.Local.Enabled)
            {
                var local = System.IO.Path.Combine(
                    workingDirectory,
                    configuration.Publishers.Local.OutputDirectory,
                    candidate);

                if (File.Exists(local))
                {
                    yield return local;
                }
            }
        }

        var readme = System.IO.Path.Combine(workingDirectory, "README.md");
        if (File.Exists(readme))
        {
            yield return readme;
        }
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
        => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(file => ValidatedExtensions.Contains(System.IO.Path.GetExtension(file), StringComparer.OrdinalIgnoreCase));
}
