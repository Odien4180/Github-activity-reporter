using GitHubActivityReporter.Bootstrap.GitHubActions;
using GitHubActivityReporter.Bootstrap.Templates;
using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Bootstrap.Generators;

public sealed record WorkflowInstallResult
{
    public required string Path { get; init; }

    public required bool Changed { get; init; }

    public required string Content { get; init; }
}

/// <summary>Writes the generated workflow file into a repository working copy.</summary>
public sealed class WorkflowInstaller
{
    private readonly WorkflowGenerator _generator;

    public WorkflowInstaller(WorkflowGenerator? generator = null)
    {
        _generator = generator ?? new WorkflowGenerator();
    }

    public async Task<WorkflowInstallResult> InstallAsync(
        ReporterConfiguration configuration,
        string repositoryPath,
        WorkflowOptions? options = null,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var content = _generator.Generate(configuration, options);
        var directory = Path.Combine(Path.GetFullPath(repositoryPath), WorkflowTemplate.RelativeDirectory);
        var path = Path.Combine(directory, WorkflowTemplate.FileName);

        if (File.Exists(path))
        {
            var current = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.Equals(current, content, StringComparison.Ordinal))
            {
                return new WorkflowInstallResult { Path = path, Changed = false, Content = content };
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        }

        return new WorkflowInstallResult { Path = path, Changed = true, Content = content };
    }
}
