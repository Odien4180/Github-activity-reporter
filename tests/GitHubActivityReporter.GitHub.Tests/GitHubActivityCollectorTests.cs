using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.GitHub.Authentication;
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

    [Fact]
    public async Task CollectAsync_excludes_configured_repositories()
    {
        var source = Substitute.For<IGitHubEventSource>();
        source.GetUserEventsAsync("example", Start, Arg.Any<CancellationToken>()).Returns(
        [
            Raw("excluded", "example/example", isPrivate: false),
            Raw("included", "example/public", isPrivate: false)
        ]);
        source.GetPublicRepositoryAsync("example/public", Arg.Any<CancellationToken>()).Returns(
            new GitHubRepositoryInfo
            {
                FullName = "example/public",
                HtmlUrl = "https://github.com/example/public"
            });
        var collector = new GitHubActivityCollector(source, new InMemoryPrivateTermRegistry());
        var request = Request() with
        {
            ExcludedRepositoryFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "example/example" }
        };

        var result = await collector.CollectAsync(request, CancellationToken.None);

        var item = Assert.Single(result.PublicEvents);
        Assert.Equal("example/public", item.RepositoryName);
        await source.Received(1).GetPublicRepositoryAsync("example/public", Arg.Any<CancellationToken>());
        await source.DidNotReceive().GetPublicRepositoryAsync("example/example", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CollectAsync_logs_source_diagnostics_when_available()
    {
        var source = Substitute.For<IGitHubEventSource>();
        source.GetUserEventsAsync("example", Start, Arg.Any<CancellationToken>()).Returns(Array.Empty<GitHubRawEvent>());
        source.LastDiagnostics.Returns("authenticated-user feed unavailable; used username-scoped fallback feed.");
        var log = new InMemoryReporterLog();
        var collector = new GitHubActivityCollector(source, new InMemoryPrivateTermRegistry(), log);

        await collector.CollectAsync(Request(), CancellationToken.None);

        Assert.Contains(log.Lines, line => line.Contains("GitHub event source diagnostics:", StringComparison.Ordinal));
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

public sealed class CollectorFactoryTests
{
    [Fact]
    public async Task CreateAsync_reports_environment_token_source()
    {
        var configuration = new ReporterConfiguration
        {
            GitHub = new GitHubSettings
            {
                Username = "example",
                TokenSecretName = "CUSTOM_TOKEN",
                ProfileRepository = new ProfileRepositorySettings
                {
                    Owner = "example",
                    Name = "example",
                    Branch = "main"
                }
            }
        };

        var cli = new GitHubCliClient(tokenProvider: new GitHubTokenProvider(
            name => name == "CUSTOM_TOKEN" ? "token" : null));
        var factory = new CollectorFactory(cli);

        var result = await factory.CreateAsync(
            configuration,
            new InMemoryPrivateTermRegistry(),
            new InMemoryReporterLog(),
            CancellationToken.None);

        Assert.Equal("environment:CUSTOM_TOKEN", result.TokenSource);
    }
}
