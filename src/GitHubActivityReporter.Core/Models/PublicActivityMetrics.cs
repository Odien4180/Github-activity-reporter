namespace GitHubActivityReporter.Core.Models;

/// <summary>Aggregated counters for a single public repository.</summary>
public sealed record PublicActivityMetrics
{
    public int CommitCount { get; init; }

    public int PullRequestOpenedCount { get; init; }

    public int PullRequestMergedCount { get; init; }

    public int PullRequestClosedCount { get; init; }

    public int IssueOpenedCount { get; init; }

    public int IssueClosedCount { get; init; }

    public int ReviewSubmittedCount { get; init; }

    public int ReleasePublishedCount { get; init; }

    public int TotalCount =>
        CommitCount
        + PullRequestOpenedCount
        + PullRequestMergedCount
        + PullRequestClosedCount
        + IssueOpenedCount
        + IssueClosedCount
        + ReviewSubmittedCount
        + ReleasePublishedCount;
}
