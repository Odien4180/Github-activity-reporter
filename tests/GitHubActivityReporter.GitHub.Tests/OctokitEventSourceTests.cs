using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.GitHub.Api;
using NSubstitute;
using Octokit;

namespace GitHubActivityReporter.GitHub.Tests;

public sealed class OctokitEventSourceTests
{
    [Fact]
    public async Task TryGetComparedCommitCountAsync_ReturnsCompareTotal()
    {
        var client = Substitute.For<IGitHubClient>();
        var repositories = Substitute.For<IRepositoriesClient>();
        var commits = Substitute.For<IRepositoryCommitsClient>();
        client.Repository.Returns(repositories);
        repositories.Commit.Returns(commits);

        var comparison = new CompareResult(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null!,
            null!,
            "ahead",
            10,
            0,
            10,
            Array.Empty<GitHubCommit>(),
            Array.Empty<GitHubCommitFile>());

        commits.Compare("owner", "repository", "before", "head")
            .Returns(Task.FromResult(comparison));

        var source = new OctokitEventSource(client);

        var count = await source.TryGetComparedCommitCountAsync(
            "owner/repository",
            "before",
            "head",
            CancellationToken.None);

        Assert.Equal(10, count);
    }

    [Theory]
    [InlineData(null, "before", "head")]
    [InlineData("invalid", "before", "head")]
    [InlineData("owner/repository", null, "head")]
    [InlineData("owner/repository", "before", null)]
    public async Task TryGetComparedCommitCountAsync_InvalidCoordinates_ReturnsNull(
        string? repository,
        string? before,
        string? head)
    {
        var source = new OctokitEventSource(Substitute.For<IGitHubClient>());

        var count = await source.TryGetComparedCommitCountAsync(
            repository,
            before,
            head,
            CancellationToken.None);

        Assert.Null(count);
    }

    [Fact]
    public async Task GetUserEventsAsync_includes_organization_scoped_private_events()
    {
        var client = Substitute.For<IGitHubClient>();
        var activities = Substitute.For<IActivitiesClient>();
        var eventsClient = Substitute.For<IEventsClient>();
        var organizations = Substitute.For<IOrganizationsClient>();
        client.Activity.Returns(activities);
        activities.Events.Returns(eventsClient);
        client.Organization.Returns(organizations);

        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");
        eventsClient.GetAllUserPerformed(
                "example-user",
                Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Activity>>(Array.Empty<Activity>()));
        organizations.GetAllForCurrent(Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Organization>>(
            [
                new Organization { Login = "example-org" }
            ]));
        eventsClient.GetAllForAnOrganization(
                "example-user",
                "example-org",
                Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Activity>>(
            [
                new Activity
                {
                    Id = "org-private-push",
                    Type = "PushEvent",
                    Public = false,
                    CreatedAt = start.AddHours(1),
                    Repo = new Repository
                    {
                        Name = "example-org/private-repository",
                        FullName = "example-org/private-repository",
                        Private = true
                    }
                }
            ]));

        var source = new OctokitEventSource(client);

        var events = await source.GetUserEventsAsync("example-user", start, CancellationToken.None);

        var commit = Assert.Single(events);
        Assert.Equal("example-org/private-repository", commit.RepositoryFullName);
        Assert.True(commit.IsPrivateRepository);
        Assert.Equal(ActivityType.Commit, commit.Type);
    }
}
