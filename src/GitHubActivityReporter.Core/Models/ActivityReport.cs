namespace GitHubActivityReporter.Core.Models;

/// <summary>The composed report that every renderer consumes.</summary>
public sealed record ActivityReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public required string GitHubUserName { get; init; }

    public required IReadOnlyList<PublicRepositoryActivity> PublicActivities { get; init; }

    public PublicActivityNarrative PublicNarrative { get; init; } = new();

    public required PrivateActivityMetrics PrivateMetrics { get; init; }

    public PublicActivityMetrics PublicTotals => new()
    {
        CommitCount = PublicActivities.Sum(a => a.Metrics.CommitCount),
        PullRequestOpenedCount = PublicActivities.Sum(a => a.Metrics.PullRequestOpenedCount),
        PullRequestMergedCount = PublicActivities.Sum(a => a.Metrics.PullRequestMergedCount),
        PullRequestClosedCount = PublicActivities.Sum(a => a.Metrics.PullRequestClosedCount),
        IssueOpenedCount = PublicActivities.Sum(a => a.Metrics.IssueOpenedCount),
        IssueClosedCount = PublicActivities.Sum(a => a.Metrics.IssueClosedCount),
        ReviewSubmittedCount = PublicActivities.Sum(a => a.Metrics.ReviewSubmittedCount),
        ReleasePublishedCount = PublicActivities.Sum(a => a.Metrics.ReleasePublishedCount)
    };

    public bool HasAnyActivity => PublicActivities.Count > 0 || PrivateMetrics.HasActivity;
}
