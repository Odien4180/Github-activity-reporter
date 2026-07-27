using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.FileSystem;

/// <summary>Writes every rendered artifact into a local output directory.</summary>
public sealed class LocalFileReportPublisher : IReportPublisher
{
    private readonly IReporterLog _log;

    public LocalFileReportPublisher(IReporterLog? log = null)
    {
        _log = log ?? NullReporterLog.Instance;
    }

    public string PublisherId => KnownPublishers.Local;

    public async Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Configuration.Publishers.Local.Enabled)
        {
            return PublishResult.Skipped(PublisherId, "Local publisher is disabled.");
        }

        if (context.DryRun)
        {
            return PublishResult.Skipped(PublisherId, "Dry run: no file was written.");
        }

        var root = Path.Combine(
            Path.GetFullPath(context.WorkingDirectory),
            context.Configuration.Publishers.Local.OutputDirectory);

        var written = new List<string>();

        foreach (var artifact in report.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = Path.Combine(root, artifact.RelativePath);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(target))
            {
                var current = await File.ReadAllTextAsync(target, cancellationToken).ConfigureAwait(false);
                if (string.Equals(current, artifact.Content, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            await File.WriteAllTextAsync(target, artifact.Content, cancellationToken).ConfigureAwait(false);
            written.Add(target);
        }

        if (written.Count == 0)
        {
            return PublishResult.NoChanges(PublisherId, "Local output is already up to date.");
        }

        _log.Info($"Wrote {written.Count} file(s) to the local output directory.");
        return PublishResult.Published(PublisherId, written);
    }
}
