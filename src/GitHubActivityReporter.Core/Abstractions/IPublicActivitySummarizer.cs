using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Abstractions;

/// <summary>
/// Summarises public activity only. There is deliberately no overload accepting
/// private activity: private data must never reach a summarizer (rule based or AI).
/// </summary>
public interface IPublicActivitySummarizer
{
    Task<PublicActivitySummary> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken);
}
