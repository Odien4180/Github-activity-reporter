namespace GitHubActivityReporter.Core.Models;

/// <summary>Supported GitHub activity types.</summary>
public enum ActivityType
{
    Commit,
    PullRequestOpened,
    PullRequestMerged,
    PullRequestClosed,
    IssueOpened,
    IssueClosed,
    ReviewSubmitted,
    ReleasePublished
}
