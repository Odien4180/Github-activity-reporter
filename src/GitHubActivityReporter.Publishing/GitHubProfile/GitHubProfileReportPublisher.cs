using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.GitHubProfile;

public sealed record GitHubProfilePublisherOptions
{
    /// <summary>Working copy of the profile repository. Defaults to the pipeline working directory.</summary>
    public string? RepositoryPath { get; init; }

    public string ReadmeFileName { get; init; } = "README.md";

    public bool Commit { get; init; }

    public bool Push { get; init; }

    public string CommitMessage { get; init; } = "chore(profile): update GitHub activity report";

    public const string RepositoryPathOption = "profile.repository-path";
    public const string CommitOption = "profile.commit";
    public const string PushOption = "profile.push";

    public static GitHubProfilePublisherOptions FromContext(PublisherContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new GitHubProfilePublisherOptions
        {
            RepositoryPath = context.Options.TryGetValue(RepositoryPathOption, out var path) && !string.IsNullOrWhiteSpace(path)
                ? path
                : context.WorkingDirectory,
            Commit = ReadFlag(context, CommitOption),
            Push = ReadFlag(context, PushOption)
        };
    }

    private static bool ReadFlag(PublisherContext context, string key)
        => context.Options.TryGetValue(key, out var value)
           && bool.TryParse(value, out var parsed)
           && parsed;
}

/// <summary>
/// Publishes the report into a GitHub profile repository working copy:
/// it writes the generated files and refreshes the README marker block.
/// The user written README content is always preserved.
/// </summary>
public sealed class GitHubProfileReportPublisher : IReportPublisher
{
    private readonly IGitCommandRunner _git;
    private readonly IReporterLog _log;

    public GitHubProfileReportPublisher(IGitCommandRunner? git = null, IReporterLog? log = null)
    {
        _git = git ?? new GitProcessRunner();
        _log = log ?? NullReporterLog.Instance;
    }

    public string PublisherId => KnownPublishers.GitHubProfile;

    public async Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Configuration.Publishers.GitHubProfile.Enabled)
        {
            return PublishResult.Skipped(PublisherId, "GitHub profile publisher is disabled.");
        }

        var options = GitHubProfilePublisherOptions.FromContext(context);
        var repositoryPath = Path.GetFullPath(options.RepositoryPath ?? context.WorkingDirectory);

        if (context.DryRun)
        {
            return PublishResult.Skipped(PublisherId, "Dry run: the profile repository was not modified.");
        }

        if (!Directory.Exists(repositoryPath))
        {
            return PublishResult.Failed(PublisherId, "The configured profile repository path does not exist.");
        }

        var changed = new List<string>();

        try
        {
            foreach (var artifact in report.Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var target = Path.Combine(repositoryPath, artifact.RelativePath);
                if (await WriteIfChangedAsync(target, artifact.Content, cancellationToken).ConfigureAwait(false))
                {
                    changed.Add(target);
                }

                if (artifact.Kind == RenderedArtifactKind.Markdown)
                {
                    var readmePath = Path.Combine(repositoryPath, options.ReadmeFileName);
                    var existing = File.Exists(readmePath)
                        ? await File.ReadAllTextAsync(readmePath, cancellationToken).ConfigureAwait(false)
                        : null;

                    var update = ReadmeMarkerUpdater.Update(existing, artifact.Content);
                    if (update.Changed
                        && await WriteIfChangedAsync(readmePath, update.Content, cancellationToken).ConfigureAwait(false))
                    {
                        changed.Add(readmePath);
                    }
                }
            }
        }
        catch (ReadmeMarkerException exception)
        {
            return PublishResult.Failed(PublisherId, exception.Message);
        }
        catch (IOException exception)
        {
            return PublishResult.Failed(PublisherId, $"Failed to write to the profile repository: {exception.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return PublishResult.Failed(PublisherId, "The profile repository path is not writable.");
        }

        if (changed.Count == 0)
        {
            _log.Info("Profile repository is already up to date, nothing was committed.");
            return PublishResult.NoChanges(PublisherId);
        }

        if (!options.Commit)
        {
            return PublishResult.Published(PublisherId, changed, "Files updated. Commit is handled by the workflow.");
        }

        var commitResult = await CommitAsync(repositoryPath, changed, options, context, cancellationToken).ConfigureAwait(false);
        return commitResult ?? PublishResult.Published(PublisherId, changed);
    }

    private async Task<PublishResult?> CommitAsync(
        string repositoryPath,
        IReadOnlyList<string> changed,
        GitHubProfilePublisherOptions options,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        var addArguments = new List<string> { "add", "--" };
        addArguments.AddRange(changed.Select(path => Path.GetRelativePath(repositoryPath, path)));

        var add = await _git.RunAsync(repositoryPath, addArguments, cancellationToken).ConfigureAwait(false);
        if (!add.Succeeded)
        {
            return PublishResult.Failed(PublisherId, "git add failed.");
        }

        var staged = await _git
            .RunAsync(repositoryPath, ["diff", "--cached", "--quiet"], cancellationToken)
            .ConfigureAwait(false);

        if (staged.ExitCode == 0)
        {
            return PublishResult.NoChanges(PublisherId, "Nothing staged, no commit was created.");
        }

        var commit = await _git
            .RunAsync(repositoryPath, ["commit", "-m", options.CommitMessage], cancellationToken)
            .ConfigureAwait(false);

        if (!commit.Succeeded)
        {
            return PublishResult.Failed(PublisherId, "git commit failed.");
        }

        if (!options.Push)
        {
            return PublishResult.Published(PublisherId, changed, "Committed locally.");
        }

        var branch = context.Configuration.GitHub.ProfileRepository.Branch;
        var push = await _git
            .RunAsync(repositoryPath, ["push", "origin", $"HEAD:{branch}"], cancellationToken)
            .ConfigureAwait(false);

        return push.Succeeded
            ? PublishResult.Published(PublisherId, changed, "Committed and pushed.")
            : PublishResult.Failed(PublisherId, "git push failed.");
    }

    private static async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            var current = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.Equals(current, content, StringComparison.Ordinal))
            {
                return false;
            }
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
