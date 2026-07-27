namespace GitHubActivityReporter.Core.Models;

/// <summary>
/// Anonymised aggregate of all private repository activity.
/// This is the only private-derived data allowed in reports and outputs.
/// </summary>
public sealed record PrivateActivityMetrics
{
    public static readonly PrivateActivityMetrics Empty = new();

    public int ActiveRepositoryCount { get; init; }

    public int CommitCount { get; init; }

    public int PullRequestOpenedCount { get; init; }

    public int PullRequestMergedCount { get; init; }

    public int PullRequestClosedCount { get; init; }

    public int IssueOpenedCount { get; init; }

    public int IssueClosedCount { get; init; }

    public int ReviewSubmittedCount { get; init; }

    public int ReleasePublishedCount { get; init; }

    public int ActiveDayCount { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public int TotalEventCount =>
        CommitCount
        + PullRequestOpenedCount
        + PullRequestMergedCount
        + PullRequestClosedCount
        + IssueOpenedCount
        + IssueClosedCount
        + ReviewSubmittedCount
        + ReleasePublishedCount;

    public bool HasActivity => TotalEventCount > 0 || ActiveRepositoryCount > 0;
}
