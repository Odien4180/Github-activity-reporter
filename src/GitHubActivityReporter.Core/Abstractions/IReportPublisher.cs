using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Abstractions;

public sealed record PublisherContext
{
    public required ReporterConfiguration Configuration { get; init; }

    /// <summary>Root directory the publisher may write to.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>When true, no external side effect (write, commit, push, send) may happen.</summary>
    public bool DryRun { get; init; }

    public IReadOnlyDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>();
}

public interface IReportPublisher
{
    string PublisherId { get; }

    Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken);
}
