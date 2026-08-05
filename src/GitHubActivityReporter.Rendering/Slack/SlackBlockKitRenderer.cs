using System.Text.Json;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Slack;

/// <summary>Creates a Slack incoming-webhook payload using Block Kit.</summary>
public sealed class SlackBlockKitRenderer : IReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string RendererId => KnownRenderers.SlackBlocks;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var blocks = new List<object>
        {
            Section($"*GitHub activity for {Escape(report.GitHubUserName)}*\n{report.PeriodStart:yyyy-MM-dd} – {report.PeriodEnd:yyyy-MM-dd}"),
            Section(BuildPublicSummary(report))
        };

        foreach (var repository in report.PublicActivities.Take(10))
        {
            blocks.Add(Section($"*{Escape(repository.RepositoryName)}*\n{Escape(repository.Summary ?? "No summary")}"));
        }

        blocks.Add(Section($"*Private (aggregate only)*\n{report.PrivateMetrics.ActiveRepositoryCount} active repositories · {report.PrivateMetrics.CommitCount} commits · {report.PrivateMetrics.PullRequestMergedCount} merged PRs"));

        var content = JsonSerializer.Serialize(new
        {
            text = $"GitHub activity for {report.GitHubUserName}",
            blocks
        }, JsonOptions);

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts =
            [
                new RenderedArtifact
                {
                    Name = "slack.json",
                    RelativePath = context.Configuration.Outputs.Slack.Target,
                    Content = content,
                    Kind = RenderedArtifactKind.Json
                }
            ]
        });
    }

    private static object Section(string text) => new
    {
        type = "section",
        text = new { type = "mrkdwn", text }
    };

    private static string BuildPublicSummary(ActivityReport report)
    {
        var lines = new List<string> { "*Public activity*" };
        if (!string.IsNullOrWhiteSpace(report.PublicNarrative.Headline))
        {
            lines.Add(Escape(report.PublicNarrative.Headline));
            lines.AddRange(report.PublicNarrative.Highlights.Select(highlight => $"• {Escape(highlight)}"));
        }
        lines.Add($"_Supporting metrics:_ {report.PublicTotals.CommitCount} commits · {report.PublicTotals.PullRequestMergedCount} merged PRs · {report.PublicTotals.IssueClosedCount} closed issues");
        return string.Join("\n", lines);
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
