using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Validation;
using GitHubActivityReporter.Rendering.Json;
using GitHubActivityReporter.Rendering.Markdown;
using GitHubActivityReporter.Summarization.RuleBased;

namespace GitHubActivityReporter.Security.Tests;

public sealed class PrivateDataLeakTests
{
    private static async Task<(ActivityReport Report, RendererContext Context, ValidationContext Validation)> BuildAsync()
    {
        var (collected, registry) = SampleActivity.Collect();
        var configuration = SampleActivity.Configuration();

        var builder = new ActivityReportBuilder(
            new RuleBasedPublicActivitySummarizer(configuration.Summary),
            new FixedClock(SampleActivity.PeriodEnd));

        var report = await builder.BuildAsync(
            collected,
            new ReportBuildContext
            {
                GitHubUserName = "example-user",
                Period = new ReportPeriod { Start = SampleActivity.PeriodStart, End = SampleActivity.PeriodEnd }
            },
            CancellationToken.None);

        return (report, RendererContext.ForConfiguration(configuration), ValidationContext.Create(registry, configuration));
    }

    [Fact]
    public async Task Markdown_output_never_contains_private_identifiers()
    {
        var (report, context, _) = await BuildAsync();

        var markdown = new MarkdownReportRenderer().Render(report, context);

        Assert.Contains(SampleActivity.PublicRepository, markdown, StringComparison.Ordinal);
        foreach (var forbidden in SampleActivity.PrivateStrings)
        {
            Assert.DoesNotContain(forbidden, markdown, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Json_output_never_contains_private_identifiers()
    {
        var (report, context, _) = await BuildAsync();

        var json = new JsonReportRenderer().Render(report, context);

        Assert.Contains(SampleActivity.PublicRepository, json, StringComparison.Ordinal);
        foreach (var forbidden in SampleActivity.PrivateStrings)
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Json_output_never_contains_the_opaque_private_repository_identifier()
    {
        var (collected, _) = SampleActivity.Collect();
        var opaqueIds = collected.PrivateEvents.Select(e => e.RepositoryOpaqueId).Distinct().ToArray();
        var (report, context, _) = await BuildAsync();

        var json = new JsonReportRenderer().Render(report, context);
        var markdown = new MarkdownReportRenderer().Render(report, context);

        Assert.NotEmpty(opaqueIds);
        foreach (var id in opaqueIds)
        {
            Assert.DoesNotContain(id, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(id, markdown, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Private_metrics_are_still_reported_as_counters()
    {
        var (report, _, _) = await BuildAsync();

        Assert.Equal(2, report.PrivateMetrics.ActiveRepositoryCount);
        Assert.Equal(1, report.PrivateMetrics.PullRequestOpenedCount);
        Assert.Equal(1, report.PrivateMetrics.IssueClosedCount);
        Assert.Equal(1, report.PrivateMetrics.CommitCount);
    }

    [Theory]
    [InlineData(SampleActivity.PrivateRepository)]
    [InlineData(SampleActivity.PrivatePullRequestTitle)]
    [InlineData(SampleActivity.PrivateIssueTitle)]
    [InlineData(SampleActivity.PrivateBranch)]
    [InlineData(SampleActivity.PrivateFilePath)]
    [InlineData(SampleActivity.PrivateCommitMessage)]
    [InlineData(SampleActivity.PrivateOrganization)]
    public async Task Privacy_validator_detects_injected_private_identifiers(string forbidden)
    {
        var (_, _, validation) = await BuildAsync();

        var result = new PrivacyValidator().ValidateContent(
            $"## Activity\n\nWorked on {forbidden} today.\n",
            "generated/activity.md",
            validation);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.RuleId == PrivacyValidator.PrivateTermRuleId);
    }

    [Fact]
    public async Task Privacy_validator_never_echoes_the_detected_private_value()
    {
        var (_, _, validation) = await BuildAsync();

        var result = new PrivacyValidator().ValidateContent(
            $"leaked {SampleActivity.PrivateRepository}",
            "generated/activity.md",
            validation);

        Assert.False(result.IsValid);
        foreach (var issue in result.Errors)
        {
            Assert.DoesNotContain(SampleActivity.PrivateRepository, issue.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Privacy_validator_accepts_the_real_rendered_output()
    {
        var (report, context, validation) = await BuildAsync();
        var validator = new PrivacyValidator();

        var markdown = await new MarkdownReportRenderer().RenderAsync(report, context, CancellationToken.None);
        var json = await new JsonReportRenderer().RenderAsync(report, context, CancellationToken.None);

        Assert.True(validator.Validate(markdown, validation).IsValid);
        Assert.True(validator.Validate(json, validation).IsValid);
    }

    [Fact]
    public void Github_token_shaped_values_are_rejected()
    {
        var result = new PrivacyValidator().ValidateContent(
            "token: ghp_" + new string('a', 36),
            "generated/report.json",
            new ValidationContext());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.RuleId == PrivacyValidator.TokenRuleId);
    }

    [Fact]
    public void Configured_secret_values_are_rejected()
    {
        var context = new ValidationContext { SecretValues = ["super-secret-value"] };

        var result = new PrivacyValidator().ValidateContent(
            "webhook credential super-secret-value",
            "generated/slack.json",
            context);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.RuleId == PrivacyValidator.SecretRuleId);
    }
}
