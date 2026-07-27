using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Publishing.FileSystem;

namespace GitHubActivityReporter.Publishing.Tests;

public sealed class LocalFileReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_writes_artifacts_and_is_idempotent()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var config = GitHubActivityReporter.Core.Configuration.ReporterConfiguration.CreateDefault("example-user");
            config.Publishers.Local.Enabled = true;
            config.Publishers.Local.OutputDirectory = "output";
            var context = PublisherTestData.Context(root, config);
            var publisher = new LocalFileReportPublisher();

            var first = await publisher.PublishAsync(PublisherTestData.MarkdownReport(), context, CancellationToken.None);
            var second = await publisher.PublishAsync(PublisherTestData.MarkdownReport(), context, CancellationToken.None);

            Assert.Equal(PublishOutcome.Published, first.Outcome);
            Assert.Equal("generated activity", await File.ReadAllTextAsync(Path.Combine(root, "output", "generated", "activity.md")));
            Assert.Equal(PublishOutcome.NoChanges, second.Outcome);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_dry_run_does_not_create_output_directory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var config = GitHubActivityReporter.Core.Configuration.ReporterConfiguration.CreateDefault("example-user");
            config.Publishers.Local.Enabled = true;
            config.Publishers.Local.OutputDirectory = "output";

            var result = await new LocalFileReportPublisher().PublishAsync(
                PublisherTestData.MarkdownReport(),
                PublisherTestData.Context(root, config, dryRun: true),
                CancellationToken.None);

            Assert.Equal(PublishOutcome.Skipped, result.Outcome);
            Assert.False(Directory.Exists(Path.Combine(root, "output")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"activity-reporter-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
