using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Core.Tests;

public sealed class PrivateActivityAggregationTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    private static CollectionRequest Request => new()
    {
        UserName = "example-user",
        PeriodStart = Start,
        PeriodEnd = Start.AddDays(7)
    };

    private static CollectedActivity Collect(params ActivityInput[] inputs)
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        builder.AddRange(inputs, Request);
        return builder.Build();
    }

    private static ActivityInput Private(ActivityType type, int dayOffset, string repository = "company/secret") => new()
    {
        Type = type,
        RepositoryFullName = repository,
        IsPrivateRepository = true,
        OccurredAt = Start.AddDays(dayOffset)
    };

    [Fact]
    public void Empty_input_produces_empty_metrics()
    {
        var metrics = new PrivateActivityAggregator().Aggregate(Collect().PrivateEvents);

        Assert.Equal(PrivateActivityMetrics.Empty, metrics);
        Assert.False(metrics.HasActivity);
    }

    [Fact]
    public void Counters_are_aggregated_per_activity_type()
    {
        var collected = Collect(
            Private(ActivityType.Commit, 1),
            Private(ActivityType.Commit, 1),
            Private(ActivityType.PullRequestOpened, 2),
            Private(ActivityType.PullRequestMerged, 3),
            Private(ActivityType.PullRequestClosed, 3),
            Private(ActivityType.IssueOpened, 3),
            Private(ActivityType.IssueClosed, 4),
            Private(ActivityType.ReviewSubmitted, 4),
            Private(ActivityType.ReleasePublished, 5));

        var metrics = new PrivateActivityAggregator().Aggregate(collected.PrivateEvents);

        Assert.Equal(2, metrics.CommitCount);
        Assert.Equal(1, metrics.PullRequestOpenedCount);
        Assert.Equal(1, metrics.PullRequestMergedCount);
        Assert.Equal(1, metrics.PullRequestClosedCount);
        Assert.Equal(1, metrics.IssueOpenedCount);
        Assert.Equal(1, metrics.IssueClosedCount);
        Assert.Equal(1, metrics.ReviewSubmittedCount);
        Assert.Equal(1, metrics.ReleasePublishedCount);
        Assert.Equal(9, metrics.TotalEventCount);
        Assert.True(metrics.HasActivity);
    }

    [Fact]
    public void Active_repository_count_uses_distinct_opaque_repositories()
    {
        var collected = Collect(
            Private(ActivityType.Commit, 1, "company/one"),
            Private(ActivityType.Commit, 1, "company/one"),
            Private(ActivityType.Commit, 2, "company/two"),
            Private(ActivityType.IssueClosed, 2, "company/three"));

        var metrics = new PrivateActivityAggregator().Aggregate(collected.PrivateEvents);

        Assert.Equal(3, metrics.ActiveRepositoryCount);
    }

    [Fact]
    public void Active_day_count_uses_distinct_utc_days()
    {
        var collected = Collect(
            Private(ActivityType.Commit, 1),
            Private(ActivityType.PullRequestOpened, 1),
            Private(ActivityType.IssueClosed, 2),
            Private(ActivityType.ReviewSubmitted, 5));

        var metrics = new PrivateActivityAggregator().Aggregate(collected.PrivateEvents);

        Assert.Equal(3, metrics.ActiveDayCount);
    }

    [Fact]
    public void Last_activity_is_the_most_recent_event()
    {
        var collected = Collect(
            Private(ActivityType.Commit, 1),
            Private(ActivityType.IssueClosed, 6),
            Private(ActivityType.ReviewSubmitted, 3));

        var metrics = new PrivateActivityAggregator().Aggregate(collected.PrivateEvents);

        Assert.Equal(Start.AddDays(6), metrics.LastActivityAt);
    }
}
