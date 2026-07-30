using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Publishing.GitHubProfile;
using NSubstitute;

namespace GitHubActivityReporter.Publishing.Tests;

public sealed class GitHubProfileReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_updates_files_and_preserves_readme_content()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Profile\n\nAbout me\n");
            var context = PublisherTestData.Context(root, options: ProfileOptions(root));

            var result = await new GitHubProfileReportPublisher().PublishAsync(
                PublisherTestData.MarkdownReport(), context, CancellationToken.None);

            Assert.Equal(PublishOutcome.Published, result.Outcome);
            var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
            Assert.StartsWith("# Profile\n\nAbout me", readme, StringComparison.Ordinal);
            Assert.Contains(ReadmeMarkerUpdater.StartMarker, readme, StringComparison.Ordinal);
            Assert.Contains("generated activity", readme, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_commit_and_push_use_configured_branch()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var git = Substitute.For<IGitCommandRunner>();
            git.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(
                    new GitCommandResult { ExitCode = 0, StandardOutput = "", StandardError = "" },
                    new GitCommandResult { ExitCode = 1, StandardOutput = "", StandardError = "" },
                    new GitCommandResult { ExitCode = 0, StandardOutput = "", StandardError = "" },
                    new GitCommandResult { ExitCode = 0, StandardOutput = "", StandardError = "" });

            var config = GitHubActivityReporter.Core.Configuration.ReporterConfiguration.CreateDefault("example-user");
            config.GitHub.ProfileRepository.Branch = "profile";
            var context = PublisherTestData.Context(root, config, options: ProfileOptions(root, commit: true, push: true));

            var result = await new GitHubProfileReportPublisher(git).PublishAsync(
                PublisherTestData.MarkdownReport(), context, CancellationToken.None);

            Assert.Equal(PublishOutcome.Published, result.Outcome);
            await git.Received(1).RunAsync(
                root,
                Arg.Is<IReadOnlyList<string>>(a => a != null && a.SequenceEqual(new[] { "push", "origin", "HEAD:profile", "--force" })),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_dry_run_does_not_modify_repository()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var result = await new GitHubProfileReportPublisher().PublishAsync(
                PublisherTestData.MarkdownReport(),
                PublisherTestData.Context(root, dryRun: true, options: ProfileOptions(root)),
                CancellationToken.None);

            Assert.Equal(PublishOutcome.Skipped, result.Outcome);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Dictionary<string, string> ProfileOptions(string root, bool commit = false, bool push = false) => new()
    {
        [GitHubProfilePublisherOptions.RepositoryPathOption] = root,
        [GitHubProfilePublisherOptions.CommitOption] = commit.ToString(),
        [GitHubProfilePublisherOptions.PushOption] = push.ToString()
    };

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"activity-reporter-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
