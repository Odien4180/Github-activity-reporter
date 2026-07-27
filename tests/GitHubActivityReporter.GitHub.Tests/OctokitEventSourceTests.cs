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
}
