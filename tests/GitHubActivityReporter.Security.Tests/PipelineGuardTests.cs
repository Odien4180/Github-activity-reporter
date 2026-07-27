using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Validation;
using GitHubActivityReporter.Rendering.Json;
using GitHubActivityReporter.Rendering.Markdown;
using GitHubActivityReporter.Summarization.RuleBased;

namespace GitHubActivityReporter.Security.Tests;

/// <summary>Publishers must never run before, or despite, a failing privacy validation.</summary>
public sealed class PipelineGuardTests
{
    private sealed class RecordingPublisher : IReportPublisher
    {
        public int Invocations { get; private set; }

        public string PublisherId => "recording";

        public Task<PublishResult> PublishAsync(
            RenderedReport report,
            PublisherContext context,
            CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(PublishResult.Published(PublisherId, [], "recorded"));
        }
    }

    private sealed class LeakingRenderer : IReportRenderer
    {
        public string RendererId => "leaking-markdown";

        public Task<RenderedReport> RenderAsync(
            ActivityReport report,
            RendererContext context,
            CancellationToken cancellationToken)
            => Task.FromResult(new RenderedReport
            {
                RendererId = RendererId,
                Artifacts =
                [
                    new RenderedArtifact
                    {
                        Name = "profile.md",
                        RelativePath = "generated/activity.md",
                        Content = $"Worked on {SampleActivity.PrivateRepository} ({SampleActivity.PrivatePullRequestTitle}).",
                        Kind = RenderedArtifactKind.Markdown
                    }
                ]
            });
    }

    private static async Task<(ActivityReport Report, PipelineOptions Options)> BuildAsync(bool preview)
    {
        var (collected, registry) = SampleActivity.Collect();
        var configuration = SampleActivity.Configuration();

        var report = await new ActivityReportBuilder(
                new RuleBasedPublicActivitySummarizer(configuration.Summary),
                new FixedClock(SampleActivity.PeriodEnd))
            .BuildAsync(
                collected,
                new ReportBuildContext
                {
                    GitHubUserName = "example-user",
                    Period = new ReportPeriod { Start = SampleActivity.PeriodStart, End = SampleActivity.PeriodEnd }
                },
                CancellationToken.None);

        return (report, new PipelineOptions
        {
            Configuration = configuration,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            PreviewMode = preview,
            ValidationContext = ValidationContext.Create(registry, configuration)
        });
    }

    [Fact]
    public async Task Publisher_is_not_called_when_privacy_validation_fails()
    {
        var (report, options) = await BuildAsync(preview: false);
        var publisher = new RecordingPublisher();

        var pipeline = new ReportPipeline([new LeakingRenderer()], [publisher], new PrivacyValidator());
        var result = await pipeline.ExecuteAsync(report, options, CancellationToken.None);

        Assert.False(result.Validation.IsValid);
        Assert.True(result.PublishingSkipped);
        Assert.Empty(result.PublishResults);
        Assert.Equal(0, publisher.Invocations);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Preview_mode_never_calls_a_publisher()
    {
        var (report, options) = await BuildAsync(preview: true);
        var publisher = new RecordingPublisher();

        var pipeline = new ReportPipeline(
            [new MarkdownReportRenderer(), new JsonReportRenderer()],
            [publisher],
            new PrivacyValidator());

        var result = await pipeline.ExecuteAsync(report, options, CancellationToken.None);

        Assert.True(result.Validation.IsValid);
        Assert.True(result.PublishingSkipped);
        Assert.Equal(0, publisher.Invocations);
        Assert.Equal(2, result.RenderedReports.Count);
    }

    [Fact]
    public async Task Publisher_runs_only_after_a_successful_validation()
    {
        var (report, options) = await BuildAsync(preview: false);
        var publisher = new RecordingPublisher();

        var pipeline = new ReportPipeline(
            [new MarkdownReportRenderer(), new JsonReportRenderer()],
            [publisher],
            new PrivacyValidator());

        var result = await pipeline.ExecuteAsync(report, options, CancellationToken.None);

        Assert.True(result.Validation.IsValid);
        Assert.False(result.PublishingSkipped);
        Assert.Equal(2, publisher.Invocations);
        Assert.True(result.Succeeded);
    }
}
