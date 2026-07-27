using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Publishing.GitHubPages;

namespace GitHubActivityReporter.Publishing.Tests;

public sealed class GitHubPagesReportPublisherTests
{
    [Fact]
    public async Task PublishAsync_stages_static_site_without_configured_source_prefix()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var config = CreateConfiguration();
            var report = StaticSiteReport();
            var publisher = new GitHubPagesReportPublisher();

            var first = await publisher.PublishAsync(report, PublisherTestData.Context(root, config), CancellationToken.None);
            var second = await publisher.PublishAsync(report, PublisherTestData.Context(root, config), CancellationToken.None);

            Assert.Equal(PublishOutcome.Published, first.Outcome);
            Assert.Equal("<html>site</html>", await File.ReadAllTextAsync(Path.Combine(root, "pages", "index.html")));
            Assert.Equal("body{}", await File.ReadAllTextAsync(Path.Combine(root, "pages", "assets", "style.css")));
            Assert.Equal(PublishOutcome.NoChanges, second.Outcome);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_skips_non_html_renderers_and_dry_runs()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var config = CreateConfiguration();
            var publisher = new GitHubPagesReportPublisher();

            var unrelated = await publisher.PublishAsync(
                PublisherTestData.MarkdownReport(),
                PublisherTestData.Context(root, config),
                CancellationToken.None);
            var dryRun = await publisher.PublishAsync(
                StaticSiteReport(),
                PublisherTestData.Context(root, config, dryRun: true),
                CancellationToken.None);

            Assert.Equal(PublishOutcome.Skipped, unrelated.Outcome);
            Assert.Equal(PublishOutcome.Skipped, dryRun.Outcome);
            Assert.False(Directory.Exists(Path.Combine(root, "pages")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_rejects_artifact_outside_website_directory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var config = CreateConfiguration();
            var report = StaticSiteReport() with
            {
                Artifacts =
                [
                    new RenderedArtifact
                    {
                        Name = "outside",
                        RelativePath = "generated/outside.html",
                        Content = "unsafe",
                        Kind = RenderedArtifactKind.Html
                    }
                ]
            };

            var result = await new GitHubPagesReportPublisher().PublishAsync(
                report,
                PublisherTestData.Context(root, config),
                CancellationToken.None);

            Assert.Equal(PublishOutcome.Failed, result.Outcome);
            Assert.False(Directory.Exists(Path.Combine(root, "pages")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ReporterConfiguration CreateConfiguration()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Website.Enabled = true;
        config.Outputs.Website.OutputDirectory = "generated/site";
        config.Publishers.GitHubPages.Enabled = true;
        config.Publishers.GitHubPages.OutputDirectory = "pages";
        return config;
    }

    private static RenderedReport StaticSiteReport() => new()
    {
        RendererId = KnownRenderers.StaticHtml,
        Artifacts =
        [
            new RenderedArtifact
            {
                Name = "index.html",
                RelativePath = "generated/site/index.html",
                Content = "<html>site</html>",
                Kind = RenderedArtifactKind.Html
            },
            new RenderedArtifact
            {
                Name = "style.css",
                RelativePath = "generated/site/assets/style.css",
                Content = "body{}",
                Kind = RenderedArtifactKind.Css
            }
        ]
    };

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"activity-reporter-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
