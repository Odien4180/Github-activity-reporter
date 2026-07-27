using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Rendering.Html;
using GitHubActivityReporter.Rendering.Svg;

namespace GitHubActivityReporter.Rendering.Tests;

public sealed class Phase2RendererTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    private static ActivityReport CreateReport() => new()
    {
        GeneratedAt = End,
        PeriodStart = Start,
        PeriodEnd = End,
        GitHubUserName = "example-user",
        PublicActivities =
        [
            new PublicRepositoryActivity
            {
                RepositoryName = "example/public-tool",
                RepositoryUrl = "https://github.com/example/public-tool",
                Description = "A public utility",
                Language = "C#",
                Topics = ["reporting"],
                Summary = "Improved configuration and stability.",
                Events =
                [
                    new PublicActivityEvent
                    {
                        Type = ActivityType.PullRequestMerged,
                        RepositoryName = "example/public-tool",
                        RepositoryUrl = "https://github.com/example/public-tool",
                        Title = "Improve configuration flow",
                        Url = "https://github.com/example/public-tool/pull/12",
                        OccurredAt = Start.AddDays(1)
                    },
                    new PublicActivityEvent
                    {
                        Type = ActivityType.IssueClosed,
                        RepositoryName = "example/public-tool",
                        RepositoryUrl = "https://github.com/example/public-tool",
                        Title = "Fix connection timeout",
                        Url = "https://github.com/example/public-tool/issues/34",
                        OccurredAt = Start.AddDays(2)
                    }
                ],
                Metrics = new PublicActivityMetrics
                {
                    CommitCount = 5,
                    PullRequestMergedCount = 1,
                    IssueClosedCount = 1
                }
            }
        ],
        PrivateMetrics = new PrivateActivityMetrics
        {
            ActiveRepositoryCount = 2,
            CommitCount = 4,
            PullRequestOpenedCount = 1,
            PullRequestMergedCount = 1,
            IssueClosedCount = 2,
            ReviewSubmittedCount = 1,
            ActiveDayCount = 3,
            LastActivityAt = End.AddDays(-1)
        }
    };

    private static RendererContext CreateContext()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Summary.Language = "en";
        config.Outputs.Dashboard.Enabled = true;
        config.Outputs.Website.Enabled = true;
        config.Outputs.Website.OutputDirectory = "generated/site";
        return RendererContext.ForConfiguration(config);
    }

    [Fact]
    public async Task Svg_renderer_produces_one_svg_artifact()
    {
        var report = CreateReport();
        var context = CreateContext() with { TargetPath = "generated/activity-dashboard.svg" };

        var rendered = await new SvgDashboardRenderer().RenderAsync(report, context, CancellationToken.None);

        Assert.Equal(KnownRenderers.SvgDashboard, rendered.RendererId);
        var artifact = Assert.Single(rendered.Artifacts);
        Assert.Equal(RenderedArtifactKind.Svg, artifact.Kind);
        Assert.Equal("generated/activity-dashboard.svg", artifact.RelativePath);
        Assert.Contains("Development Activity", artifact.Content, StringComparison.Ordinal);
        Assert.Contains("viewBox=", artifact.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Static_html_renderer_produces_site_bundle()
    {
        var report = CreateReport();
        var context = CreateContext() with { TargetPath = "generated/site/index.html" };

        var rendered = await new StaticHtmlReportRenderer().RenderAsync(report, context, CancellationToken.None);

        Assert.Equal(KnownRenderers.StaticHtml, rendered.RendererId);
        Assert.Equal(5, rendered.Artifacts.Count);
        Assert.Contains(rendered.Artifacts, a => a.RelativePath == "generated/site/index.html" && a.Kind == RenderedArtifactKind.Html);
        Assert.Contains(rendered.Artifacts, a => a.RelativePath == "generated/site/assets/style.css" && a.Kind == RenderedArtifactKind.Css);
        Assert.Contains(rendered.Artifacts, a => a.RelativePath == "generated/site/assets/app.js" && a.Kind == RenderedArtifactKind.JavaScript);
        Assert.Contains(rendered.Artifacts, a => a.RelativePath == "generated/site/data/latest.json" && a.Kind == RenderedArtifactKind.Json);
        Assert.Contains(rendered.Artifacts, a => a.RelativePath == "generated/site/data/history.json" && a.Kind == RenderedArtifactKind.Json);
    }
}
