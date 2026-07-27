using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.GitHubPages;

/// <summary>
/// Stages the static website bundle in the directory consumed by GitHub's
/// upload-pages-artifact action. Deployment itself is performed by the workflow.
/// </summary>
public sealed class GitHubPagesReportPublisher : IReportPublisher
{
    private readonly IReporterLog _log;

    public GitHubPagesReportPublisher(IReporterLog? log = null)
    {
        _log = log ?? NullReporterLog.Instance;
    }

    public string PublisherId => KnownPublishers.GitHubPages;

    public async Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Configuration.Publishers.GitHubPages.Enabled)
        {
            return PublishResult.Skipped(PublisherId, "GitHub Pages publisher is disabled.");
        }

        if (!string.Equals(report.RendererId, KnownRenderers.StaticHtml, StringComparison.OrdinalIgnoreCase))
        {
            return PublishResult.Skipped(PublisherId, "Only the static HTML report is published to GitHub Pages.");
        }

        if (context.DryRun)
        {
            return PublishResult.Skipped(PublisherId, "Dry run: the GitHub Pages staging directory was not modified.");
        }

        var workingRoot = Path.GetFullPath(context.WorkingDirectory);
        var pagesRoot = Path.GetFullPath(Path.Combine(
            workingRoot,
            context.Configuration.Publishers.GitHubPages.OutputDirectory));
        EnsureChildPath(workingRoot, pagesRoot, "GitHub Pages output directory");

        var websiteRoot = NormalizeRelativePath(context.Configuration.Outputs.Website.OutputDirectory).TrimEnd('/');
        var written = new List<string>();

        try
        {
            foreach (var artifact in report.Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = NormalizeRelativePath(artifact.RelativePath);
                if (!relativePath.StartsWith(websiteRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return PublishResult.Failed(
                        PublisherId,
                        $"Static website artifact '{artifact.RelativePath}' is outside outputs.website.output_directory.");
                }

                var pagesRelativePath = relativePath[(websiteRoot.Length + 1)..];
                var target = Path.GetFullPath(Path.Combine(pagesRoot, pagesRelativePath));
                EnsureChildPath(pagesRoot, target, "GitHub Pages artifact");

                if (await WriteIfChangedAsync(target, artifact.Content, cancellationToken).ConfigureAwait(false))
                {
                    written.Add(target);
                }
            }
        }
        catch (IOException exception)
        {
            return PublishResult.Failed(PublisherId, $"Failed to stage GitHub Pages output: {exception.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return PublishResult.Failed(PublisherId, "The GitHub Pages staging directory is not writable.");
        }

        if (written.Count == 0)
        {
            return PublishResult.NoChanges(PublisherId, "GitHub Pages output is already up to date.");
        }

        _log.Info($"Staged {written.Count} GitHub Pages file(s).");
        return PublishResult.Published(PublisherId, written, "Static site staged for GitHub Pages deployment.");
    }

    private static async Task<bool> WriteIfChangedAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path)
            && string.Equals(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), content, StringComparison.Ordinal))
        {
            return false;
        }

        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static void EnsureChildPath(string root, string candidate, string description)
    {
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"{description} must stay inside '{root}'.");
        }
    }
}
