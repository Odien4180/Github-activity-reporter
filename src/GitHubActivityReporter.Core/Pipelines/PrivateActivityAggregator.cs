using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Pipelines;

/// <summary>Turns private raw events into anonymous counters. Internal by design.</summary>
internal sealed class PrivateActivityAggregator : IPrivateActivityAggregator
{
    public PrivateActivityMetrics Aggregate(IReadOnlyList<PrivateActivityEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return PrivateActivityMetrics.Empty;
        }

        var activeRepositories = new HashSet<string>(StringComparer.Ordinal);
        var activeDays = new HashSet<DateOnly>();
        var counts = new Dictionary<ActivityType, int>();
        DateTimeOffset? last = null;

        foreach (var item in events)
        {
            activeRepositories.Add(item.RepositoryOpaqueId);
            activeDays.Add(DateOnly.FromDateTime(item.OccurredAt.UtcDateTime));
            counts[item.Type] = counts.GetValueOrDefault(item.Type) + 1;

            if (last is null || item.OccurredAt > last)
            {
                last = item.OccurredAt;
            }
        }

        return new PrivateActivityMetrics
        {
            ActiveRepositoryCount = activeRepositories.Count,
            ActiveDayCount = activeDays.Count,
            CommitCount = counts.GetValueOrDefault(ActivityType.Commit),
            PullRequestOpenedCount = counts.GetValueOrDefault(ActivityType.PullRequestOpened),
            PullRequestMergedCount = counts.GetValueOrDefault(ActivityType.PullRequestMerged),
            PullRequestClosedCount = counts.GetValueOrDefault(ActivityType.PullRequestClosed),
            IssueOpenedCount = counts.GetValueOrDefault(ActivityType.IssueOpened),
            IssueClosedCount = counts.GetValueOrDefault(ActivityType.IssueClosed),
            ReviewSubmittedCount = counts.GetValueOrDefault(ActivityType.ReviewSubmitted),
            ReleasePublishedCount = counts.GetValueOrDefault(ActivityType.ReleasePublished),
            LastActivityAt = last
        };
    }
}
