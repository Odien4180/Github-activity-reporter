using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Abstractions;

public interface IActivityCollector
{
    Task<CollectedActivity> CollectAsync(
        CollectionRequest request,
        CancellationToken cancellationToken);
}
