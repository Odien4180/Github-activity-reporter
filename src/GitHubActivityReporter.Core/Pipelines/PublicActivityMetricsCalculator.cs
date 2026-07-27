using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Pipelines;

public static class PublicActivityMetricsCalculator
{
    public static PublicActivityMetrics Calculate(IEnumerable<PublicActivityEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var counts = new Dictionary<ActivityType, int>();
        foreach (var item in events)
        {
            counts[item.Type] = counts.GetValueOrDefault(item.Type) + 1;
        }

        return new PublicActivityMetrics
        {
            CommitCount = counts.GetValueOrDefault(ActivityType.Commit),
            PullRequestOpenedCount = counts.GetValueOrDefault(ActivityType.PullRequestOpened),
            PullRequestMergedCount = counts.GetValueOrDefault(ActivityType.PullRequestMerged),
            PullRequestClosedCount = counts.GetValueOrDefault(ActivityType.PullRequestClosed),
            IssueOpenedCount = counts.GetValueOrDefault(ActivityType.IssueOpened),
            IssueClosedCount = counts.GetValueOrDefault(ActivityType.IssueClosed),
            ReviewSubmittedCount = counts.GetValueOrDefault(ActivityType.ReviewSubmitted),
            ReleasePublishedCount = counts.GetValueOrDefault(ActivityType.ReleasePublished)
        };
    }
}
