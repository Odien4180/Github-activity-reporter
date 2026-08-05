using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.GitHub.Api;
using Octokit;

namespace GitHubActivityReporter.GitHub.Mapping;

/// <summary>Maps Octokit activity feed entries to normalised raw events.</summary>
internal static class OctokitActivityMapper
{
    public static IEnumerable<GitHubRawEvent> Map(Activity activity, PushCompareResult? compareResult = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var repositoryName = activity.Repo?.Name;
        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            yield break;
        }

        var isPrivate = !activity.Public;
        var occurredAt = activity.CreatedAt;
        var id = string.IsNullOrWhiteSpace(activity.Id) ? Guid.NewGuid().ToString("N") : activity.Id;

        switch (activity.Type)
        {
            case "PushEvent":
                foreach (var mapped in MapPush(
                             activity,
                             id,
                             repositoryName!,
                             isPrivate,
                             occurredAt,
                             compareResult))
                {
                    yield return mapped;
                }

                break;

            case "PullRequestEvent":
                if (activity.Payload is PullRequestEventPayload pullRequest)
                {
                    var type = ResolvePullRequestType(pullRequest);
                    if (type is not null)
                    {
                        yield return new GitHubRawEvent
                        {
                            Id = id,
                            Type = type.Value,
                            RepositoryFullName = repositoryName!,
                            IsPrivateRepository = isPrivate,
                            OccurredAt = occurredAt,
                            Title = isPrivate ? null : pullRequest.PullRequest?.Title,
                            Url = isPrivate ? null : pullRequest.PullRequest?.HtmlUrl
                        };
                    }
                }

                break;

            case "IssuesEvent":
                if (activity.Payload is IssueEventPayload issue)
                {
                    var type = issue.Action switch
                    {
                        "opened" => ActivityType.IssueOpened,
                        "closed" => ActivityType.IssueClosed,
                        _ => (ActivityType?)null
                    };

                    if (type is not null)
                    {
                        yield return new GitHubRawEvent
                        {
                            Id = id,
                            Type = type.Value,
                            RepositoryFullName = repositoryName!,
                            IsPrivateRepository = isPrivate,
                            OccurredAt = occurredAt,
                            Title = isPrivate ? null : issue.Issue?.Title,
                            Url = isPrivate ? null : issue.Issue?.HtmlUrl
                        };
                    }
                }

                break;

            case "PullRequestReviewEvent":
                yield return new GitHubRawEvent
                {
                    Id = id,
                    Type = ActivityType.ReviewSubmitted,
                    RepositoryFullName = repositoryName!,
                    IsPrivateRepository = isPrivate,
                    OccurredAt = occurredAt,
                    Title = null,
                    Url = null
                };

                break;

            case "ReleaseEvent":
                if (activity.Payload is ReleaseEventPayload release && release.Action == "published")
                {
                    yield return new GitHubRawEvent
                    {
                        Id = id,
                        Type = ActivityType.ReleasePublished,
                        RepositoryFullName = repositoryName!,
                        IsPrivateRepository = isPrivate,
                        OccurredAt = occurredAt,
                        Title = isPrivate ? null : (release.Release?.Name ?? release.Release?.TagName),
                        Url = isPrivate ? null : release.Release?.HtmlUrl
                    };
                }

                break;
        }
    }

    private static IEnumerable<GitHubRawEvent> MapPush(
        Activity activity,
        string id,
        string repositoryName,
        bool isPrivate,
        DateTimeOffset occurredAt,
        PushCompareResult? compareResult)
    {
        var payload = activity.Payload as PushEventPayload;
        var commits = payload?.Commits?.ToArray() ?? Array.Empty<Commit>();
        var count = ResolvePushEventCount(commits.Length, (int)(payload?.Size ?? 0), compareResult?.CommitCount);

        // Diff statistics apply to the whole push, not individual commits. Attach them to
        // the first event so the AI summarizer gets them as context.
        var attachDiff = !isPrivate && compareResult is not null;

        for (var index = 0; index < count; index++)
        {
            string? title = null;
            string? url = null;

            if (!isPrivate && index < commits.Length)
            {
                title = FirstLine(commits[index].Message);
                var sha = commits[index].Sha;
                if (!string.IsNullOrWhiteSpace(sha))
                {
                    // Only the abbreviated sha is kept: full hashes are rejected by the privacy validator.
                    url = $"https://github.com/{repositoryName}/commit/{sha[..Math.Min(7, sha.Length)]}";
                }
            }

            yield return new GitHubRawEvent
            {
                Id = $"{id}#{index}",
                Type = ActivityType.Commit,
                RepositoryFullName = repositoryName,
                IsPrivateRepository = isPrivate,
                OccurredAt = occurredAt,
                Title = title,
                Url = url,
                // Diff context is only attached to the first commit event so counts are not
                // inflated when there are multiple commits in a single push.
                ChangedPaths = attachDiff && index == 0 ? compareResult!.ChangedPaths : Array.Empty<string>(),
                Additions = attachDiff && index == 0 ? compareResult!.Additions : null,
                Deletions = attachDiff && index == 0 ? compareResult!.Deletions : null,
                ChangedFiles = attachDiff && index == 0 ? compareResult!.ChangedFiles : null
            };
        }
    }

    internal static int ResolvePushEventCount(
        int includedCommitCount,
        int reportedSize,
        int? comparedCommitCount = null)
    {
        if (comparedCommitCount is > 0)
        {
            return comparedCommitCount.Value;
        }

        if (includedCommitCount > 0)
        {
            return includedCommitCount;
        }

        if (reportedSize > 0)
        {
            return reportedSize;
        }

        // GitHub may omit both commits and size from Events API push payloads.
        // Preserve the activity as one opaque commit instead of dropping it.
        return 1;
    }

    private static ActivityType? ResolvePullRequestType(PullRequestEventPayload payload) => payload.Action switch
    {
        "opened" => ActivityType.PullRequestOpened,
        "closed" when payload.PullRequest?.Merged == true => ActivityType.PullRequestMerged,
        "closed" => ActivityType.PullRequestClosed,
        _ => null
    };

    private static string? FirstLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var index = message.IndexOfAny(['\r', '\n']);
        return (index < 0 ? message : message[..index]).Trim();
    }
}
