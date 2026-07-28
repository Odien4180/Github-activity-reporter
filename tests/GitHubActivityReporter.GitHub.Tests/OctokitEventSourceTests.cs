using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.GitHub.Api;
using NSubstitute;
using Octokit;
using System.Net;

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

        var source = new OctokitEventSource(client, apiConnection: Substitute.For<IApiConnection>());

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
        var source = new OctokitEventSource(
            Substitute.For<IGitHubClient>(),
            apiConnection: Substitute.For<IApiConnection>());

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
        var connection = Substitute.For<IApiConnection>();
        var activities = Substitute.For<IActivitiesClient>();
        var eventsClient = Substitute.For<IEventsClient>();
        var organizations = Substitute.For<IOrganizationsClient>();
        client.Activity.Returns(activities);
        activities.Events.Returns(eventsClient);
        client.Organization.Returns(organizations);

        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");
        connection.GetAll<Activity>(
                Arg.Is<Uri>(uri => uri != null && uri.ToString() == "user/events"),
                Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Activity>>(Array.Empty<Activity>()));
        organizations.GetAllForCurrent(Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Organization>>(
            [
                CreateOrganization("example-org")
            ]));
        var organizationEvents = Task.FromResult<IReadOnlyList<Activity>>(
        [
            new Activity(
                "PushEvent",
                false,
                CreateRepository("example-org/private-repository", isPrivate: true),
                null!,
                null!,
                start.AddHours(1),
                "org-private-push",
                null!)
        ]);
        eventsClient.GetAllForAnOrganization(
                "example-user",
                "example-org",
                Arg.Any<ApiOptions>())
            .Returns(
                organizationEvents,
                Task.FromResult<IReadOnlyList<Activity>>(Array.Empty<Activity>()));

        var source = new OctokitEventSource(client, apiConnection: connection);

        var events = await source.GetUserEventsAsync("example-user", start, CancellationToken.None);

        var commit = Assert.Single(events);
        Assert.Equal("example-org/private-repository", commit.RepositoryFullName);
        Assert.True(commit.IsPrivateRepository);
        Assert.Equal(ActivityType.Commit, commit.Type);
    }

    [Fact]
    public async Task GetUserEventsAsync_prefers_authenticated_user_feed_for_private_events()
    {
        var client = Substitute.For<IGitHubClient>();
        var connection = Substitute.For<IApiConnection>();
        var activities = Substitute.For<IActivitiesClient>();
        var eventsClient = Substitute.For<IEventsClient>();
        var organizations = Substitute.For<IOrganizationsClient>();
        client.Activity.Returns(activities);
        activities.Events.Returns(eventsClient);
        client.Organization.Returns(organizations);

        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");
        connection.GetAll<Activity>(
                Arg.Is<Uri>(uri => uri != null && uri.ToString() == "user/events"),
                Arg.Any<ApiOptions>())
            .Returns(
                Task.FromResult<IReadOnlyList<Activity>>(
                [
                    new Activity(
                        "PushEvent",
                        false,
                        CreateRepository("example-user/private-repository", isPrivate: true),
                        null!,
                        null!,
                        start.AddHours(1),
                        "private-push",
                        null!)
                ]),
                Task.FromResult<IReadOnlyList<Activity>>(Array.Empty<Activity>()));
        organizations.GetAllForCurrent(Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Organization>>(Array.Empty<Organization>()));

        var source = new OctokitEventSource(client, apiConnection: connection);

        var events = await source.GetUserEventsAsync("example-user", start, CancellationToken.None);

        var commit = Assert.Single(events);
        Assert.Equal("example-user/private-repository", commit.RepositoryFullName);
        Assert.True(commit.IsPrivateRepository);
        await eventsClient.DidNotReceive().GetAllUserPerformed("example-user", Arg.Any<ApiOptions>());
    }

    [Fact]
    public async Task GetUserEventsAsync_falls_back_to_username_feed_when_authenticated_route_is_unavailable()
    {
        var client = Substitute.For<IGitHubClient>();
        var connection = Substitute.For<IApiConnection>();
        var activities = Substitute.For<IActivitiesClient>();
        var eventsClient = Substitute.For<IEventsClient>();
        var organizations = Substitute.For<IOrganizationsClient>();
        client.Activity.Returns(activities);
        activities.Events.Returns(eventsClient);
        client.Organization.Returns(organizations);

        var start = DateTimeOffset.Parse("2026-07-26T00:00:00Z");
        connection.GetAll<Activity>(
                Arg.Is<Uri>(uri => uri != null && uri.ToString() == "user/events"),
                Arg.Any<ApiOptions>())
            .Returns<Task<IReadOnlyList<Activity>>>(_ => throw new ApiException("not found", HttpStatusCode.NotFound));
        eventsClient.GetAllUserPerformed(
                "example-user",
                Arg.Any<ApiOptions>())
            .Returns(
                Task.FromResult<IReadOnlyList<Activity>>(
                [
                    new Activity(
                        "PushEvent",
                        true,
                        CreateRepository("example-user/public-repository", isPrivate: false),
                        null!,
                        null!,
                        start.AddHours(1),
                        "public-push",
                        null!)
                ]),
                Task.FromResult<IReadOnlyList<Activity>>(Array.Empty<Activity>()));
        organizations.GetAllForCurrent(Arg.Any<ApiOptions>())
            .Returns(Task.FromResult<IReadOnlyList<Organization>>(Array.Empty<Organization>()));

        var source = new OctokitEventSource(client, apiConnection: connection);

        var events = await source.GetUserEventsAsync("example-user", start, CancellationToken.None);

        var commit = Assert.Single(events);
        Assert.Equal("example-user/public-repository", commit.RepositoryFullName);
        Assert.False(commit.IsPrivateRepository);
        await eventsClient.Received(2).GetAllUserPerformed("example-user", Arg.Any<ApiOptions>());
    }

    private static Organization CreateOrganization(string login) => new(
        avatarUrl: string.Empty,
        bio: string.Empty,
        blog: string.Empty,
        collaborators: 0,
        company: string.Empty,
        createdAt: DateTimeOffset.UnixEpoch,
        diskUsage: 0,
        email: string.Empty,
        followers: 0,
        following: 0,
        hireable: null,
        htmlUrl: string.Empty,
        totalPrivateRepos: 0,
        id: 1,
        nodeId: "ORG_node",
        location: string.Empty,
        login: login,
        name: login,
        ownedPrivateRepos: 0,
        plan: null!,
        privateGists: 0,
        publicGists: 0,
        publicRepos: 0,
        url: string.Empty,
        billingAddress: string.Empty,
        reposUrl: string.Empty,
        eventsUrl: string.Empty,
        hooksUrl: string.Empty,
        issuesUrl: string.Empty,
        membersUrl: string.Empty,
        publicMembersUrl: string.Empty,
        description: string.Empty,
        isVerified: false,
        hasOrganizationProjects: false,
        hasRepositoryProjects: false,
        updatedAt: DateTimeOffset.UnixEpoch);

    private static Repository CreateRepository(string fullName, bool isPrivate)
    {
        var separator = fullName.IndexOf('/');
        var owner = fullName[..separator];
        var name = fullName[(separator + 1)..];

        return new Repository(
            url: $"https://api.github.com/repos/{owner}/{name}",
            htmlUrl: $"https://github.com/{fullName}",
            cloneUrl: string.Empty,
            gitUrl: string.Empty,
            sshUrl: string.Empty,
            svnUrl: string.Empty,
            mirrorUrl: string.Empty,
            archiveUrl: string.Empty,
            id: 1,
            nodeId: "REPO_node",
            owner: null!,
            name: fullName,
            fullName: fullName,
            isTemplate: false,
            defaultBranch: "main",
            description: string.Empty,
            homepage: string.Empty,
            language: string.Empty,
            @private: isPrivate,
            fork: false,
            forksCount: 0,
            stargazersCount: 0,
            openIssuesCount: 0,
            pushedAt: DateTimeOffset.UnixEpoch,
            createdAt: DateTimeOffset.UnixEpoch,
            updatedAt: DateTimeOffset.UnixEpoch,
            permissions: null!,
            parent: null!,
            source: null!,
            license: null!,
            hasDiscussions: false,
            hasIssues: true,
            hasWiki: true,
            hasDownloads: true,
            hasPages: false,
            subscribersCount: 0,
            size: 0,
            allowRebaseMerge: null,
            allowSquashMerge: null,
            allowMergeCommit: null,
            archived: false,
            watchersCount: 0,
            deleteBranchOnMerge: false,
            visibility: isPrivate ? RepositoryVisibility.Private : RepositoryVisibility.Public,
            topics: Array.Empty<string>(),
            allowAutoMerge: null,
            allowUpdateBranch: null,
            webCommitSignoffRequired: null,
            securityAndAnalysis: null!);
    }
}
