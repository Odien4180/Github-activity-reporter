using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Rendering.Html;
using GitHubActivityReporter.Rendering.Markdown;
using GitHubActivityReporter.Rendering.Svg;
using System.Security.Cryptography;
using System.Text;

namespace GitHubActivityReporter.Rendering.Tests;

public sealed class Phase2RendererTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    internal static ActivityReport CreateReport() => new()
    {
        GeneratedAt = End,
        PeriodStart = Start,
        PeriodEnd = End,
        GitHubUserName = "example-user",
        PublicNarrative = new PublicActivityNarrative
        {
            Headline = "Improved delivery reliability across the reporting workflow.",
            Highlights =
            [
                "Strengthened configuration and timeout handling.",
                "Completed pull request delivery and issue resolution work."
            ]
        },
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

    internal static RendererContext CreateContext()
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
        Assert.Contains("Development Pulse", artifact.Content, StringComparison.Ordinal);
        Assert.Contains("viewBox=", artifact.Content, StringComparison.Ordinal);
        Assert.Contains("linearGradient id=\"accent\"", artifact.Content, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: dark", artifact.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_renderer_embeds_dashboard_and_metric_overview()
    {
        var report = CreateReport();
        var context = CreateContext();

        var markdown = new MarkdownReportRenderer().Render(report, context);

        Assert.Contains("## Development Pulse", markdown, StringComparison.Ordinal);
        Assert.Contains("<img src=\"./generated/activity-dashboard.svg\"", markdown, StringComparison.Ordinal);
        Assert.Contains("| Public repositories | Public commits | Private repositories | Private commits |", markdown, StringComparison.Ordinal);
        Assert.Contains("#### [example/public-tool](https://github.com/example/public-tool)", markdown, StringComparison.Ordinal);
        Assert.Contains("#### Activity Summary", markdown, StringComparison.Ordinal);
        Assert.Contains("Improved delivery reliability", markdown, StringComparison.Ordinal);
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

    [Fact]
    public async Task Svg_snapshot_matches_approved_content()
    {
        var rendered = await new SvgDashboardRenderer().RenderAsync(
            CreateReport(),
            CreateContext() with { TargetPath = "generated/activity-dashboard.svg" },
            CancellationToken.None);

        Assert.Equal(
            "generated/activity-dashboard.svg 36ac2ff99eaf3b4ef8c8ef582ef018a75a5c76f58e2ed09a15896f62ac4c5d05",
            SnapshotManifest(rendered));
    }

    [Fact]
    public async Task Static_site_snapshot_matches_approved_bundle()
    {
        var rendered = await new StaticHtmlReportRenderer().RenderAsync(
            CreateReport(),
            CreateContext() with { TargetPath = "generated/site/index.html" },
            CancellationToken.None);

        const string approved =
            """
            generated/site/assets/app.js 245cdbf46161cd25fb5f24861cf61aa1580884ab91a18b3a64976f47140d2388
            generated/site/assets/style.css 59b2b9dcff2420d8b3bbd09015fdd7b444addf35b68b92b60b994fb09ae3d122
            generated/site/data/history.json 61a7eaf22dadd677672a9ad136c6e9562e662ac8cb0fe441c32f807bda752ba7
            generated/site/data/latest.json a0d89746a1e511582daf19ab15fd7f65d589dece364393c47b5622d1f1a44d4e
            generated/site/index.html 5fd2b1b8f0b2125e10de8cec5c01f60485adca3635d48f2eae7872e12542d022
            """;

        Assert.Equal(approved, SnapshotManifest(rendered));
    }

    private static string SnapshotManifest(RenderedReport report)
        => string.Join(
            "\n",
            report.Artifacts
                .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
                .Select(artifact => $"{artifact.RelativePath} {Hash(artifact.Content)}"));

    private static string Hash(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
