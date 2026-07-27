using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.GitHub.Api;
using GitHubActivityReporter.GitHub.Collectors;
using NSubstitute;

namespace GitHubActivityReporter.GitHub.Tests;

public sealed class GitHubActivityCollectorTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");
    private static readonly DateTimeOffset End = DateTimeOffset.Parse("2026-07-27T00:00:00Z");

    [Fact]
    public async Task CollectAsync_deduplicates_events_and_never_enriches_private_repositories()
    {
        var source = Substitute.For<IGitHubEventSource>();
        source.GetUserEventsAsync("example", Start, Arg.Any<CancellationToken>()).Returns(
        [
            Raw("public-1", "example/public", isPrivate: false),
            Raw("public-1", "example/public", isPrivate: false),
            Raw("private-1", "secret/private", isPrivate: true)
        ]);
        source.GetPublicRepositoryAsync("example/public", Arg.Any<CancellationToken>()).Returns(
            new GitHubRepositoryInfo
            {
                FullName = "example/public",
                HtmlUrl = "https://github.com/example/public",
                Description = "public description",
                Language = "C#"
            });
        var privateTerms = new InMemoryPrivateTermRegistry();
        var collector = new GitHubActivityCollector(source, privateTerms);

        var result = await collector.CollectAsync(Request(), CancellationToken.None);

        Assert.Single(result.PublicEvents);
        Assert.Equal(1, result.PrivateEventCount);
        Assert.Contains("secret/private", privateTerms.Terms);
        await source.Received(1).GetPublicRepositoryAsync("example/public", Arg.Any<CancellationToken>());
        await source.DidNotReceive().GetPublicRepositoryAsync("secret/private", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CollectAsync_honors_period_and_visibility_switches_with_mock_source()
    {
        var source = Substitute.For<IGitHubEventSource>();
        source.GetUserEventsAsync("example", Start, Arg.Any<CancellationToken>()).Returns(
        [
            Raw("inside-public", "example/public", isPrivate: false),
            Raw("outside-public", "example/old", isPrivate: false, occurredAt: Start.AddSeconds(-1)),
            Raw("inside-private", "secret/private", isPrivate: true)
        ]);
        source.GetPublicRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((GitHubRepositoryInfo?)null);
        var collector = new GitHubActivityCollector(source, new InMemoryPrivateTermRegistry());
        var request = Request() with { CollectPrivate = false };

        var result = await collector.CollectAsync(request, CancellationToken.None);

        Assert.Single(result.PublicEvents);
        Assert.Empty(result.PrivateEvents);
        await source.DidNotReceive().GetPublicRepositoryAsync("example/old", Arg.Any<CancellationToken>());
    }

    private static CollectionRequest Request() => new()
    {
        UserName = "example",
        PeriodStart = Start,
        PeriodEnd = End,
        CollectPublic = true,
        CollectPrivate = true,
        PublicEventTypes = new HashSet<ActivityType> { ActivityType.Commit },
        PrivateEventTypes = new HashSet<ActivityType> { ActivityType.Commit }
    };

    private static GitHubRawEvent Raw(
        string id,
        string repository,
        bool isPrivate,
        DateTimeOffset? occurredAt = null) => new()
    {
        Id = id,
        Type = ActivityType.Commit,
        RepositoryFullName = repository,
        IsPrivateRepository = isPrivate,
        OccurredAt = occurredAt ?? Start.AddHours(1)
    };
}
