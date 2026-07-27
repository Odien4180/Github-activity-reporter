using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Abstractions;

/// <summary>
/// Aggregates private raw events into anonymous counters.
/// Internal by design: no consumer outside the pipeline may touch private events.
/// </summary>
internal interface IPrivateActivityAggregator
{
    PrivateActivityMetrics Aggregate(IReadOnlyList<PrivateActivityEvent> events);
}
