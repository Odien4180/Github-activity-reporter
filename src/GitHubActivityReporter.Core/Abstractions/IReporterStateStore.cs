using GitHubActivityReporter.Core.State;

namespace GitHubActivityReporter.Core.Abstractions;

public interface IReporterStateStore
{
    Task<ReporterState?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(ReporterState state, CancellationToken cancellationToken);
}
