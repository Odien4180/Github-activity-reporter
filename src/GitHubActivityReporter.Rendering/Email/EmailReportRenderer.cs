using System.Net;
using System.Text;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Email;

/// <summary>Creates a multipart-friendly HTML and plain-text email pair.</summary>
public sealed class EmailReportRenderer : IReportRenderer
{
    public string RendererId => KnownRenderers.EmailHtml;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts =
            [
                new RenderedArtifact
                {
                    Name = "email.html",
                    RelativePath = context.Configuration.Outputs.Email.HtmlTarget,
                    Content = BuildHtml(report),
                    Kind = RenderedArtifactKind.Html
                },
                new RenderedArtifact
                {
                    Name = "email.txt",
                    RelativePath = context.Configuration.Outputs.Email.TextTarget,
                    Content = BuildText(report),
                    Kind = RenderedArtifactKind.PlainText
                }
            ]
        });
    }

    private static string BuildHtml(ActivityReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html><body style=\"font-family:Arial,sans-serif;color:#24292f\">");
        html.AppendLine($"<h1>GitHub activity for {WebUtility.HtmlEncode(report.GitHubUserName)}</h1>");
        html.AppendLine($"<p>{report.PeriodStart:yyyy-MM-dd} – {report.PeriodEnd:yyyy-MM-dd}</p>");
        html.AppendLine("<h2>Public activity</h2>");
        if (!string.IsNullOrWhiteSpace(report.PublicNarrative.Headline))
        {
            html.AppendLine($"<p><strong>{WebUtility.HtmlEncode(report.PublicNarrative.Headline)}</strong></p>");
            html.AppendLine("<ul>");
            foreach (var highlight in report.PublicNarrative.Highlights)
            {
                html.AppendLine($"<li>{WebUtility.HtmlEncode(highlight)}</li>");
            }
            html.AppendLine("</ul>");
        }
        html.AppendLine($"<p><small>Supporting metrics: {report.PublicTotals.CommitCount} commits · {report.PublicTotals.PullRequestMergedCount} merged pull requests · {report.PublicTotals.IssueClosedCount} closed issues</small></p>");
        foreach (var repository in report.PublicActivities)
        {
            html.AppendLine($"<h3>{WebUtility.HtmlEncode(repository.RepositoryName)}</h3>");
            if (!string.IsNullOrWhiteSpace(repository.Summary))
            {
                html.AppendLine($"<p>{WebUtility.HtmlEncode(repository.Summary)}</p>");
            }
        }

        html.AppendLine("<h2>Private activity (aggregate only)</h2>");
        html.AppendLine($"<p>{report.PrivateMetrics.ActiveRepositoryCount} active repositories · {report.PrivateMetrics.CommitCount} commits · {report.PrivateMetrics.PullRequestMergedCount} merged pull requests</p>");
        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static string BuildText(ActivityReport report)
    {
        var text = new StringBuilder();
        text.AppendLine($"GitHub activity for {report.GitHubUserName}");
        text.AppendLine($"{report.PeriodStart:yyyy-MM-dd} - {report.PeriodEnd:yyyy-MM-dd}");
        text.AppendLine();
        text.AppendLine("Public activity");
        if (!string.IsNullOrWhiteSpace(report.PublicNarrative.Headline))
        {
            text.AppendLine(report.PublicNarrative.Headline);
            foreach (var highlight in report.PublicNarrative.Highlights)
            {
                text.AppendLine($"- {highlight}");
            }
        }
        text.AppendLine($"Supporting metrics: {report.PublicTotals.CommitCount} commits, {report.PublicTotals.PullRequestMergedCount} merged pull requests, {report.PublicTotals.IssueClosedCount} closed issues");
        foreach (var repository in report.PublicActivities)
        {
            text.AppendLine($"- {repository.RepositoryName}: {repository.Summary}");
        }

        text.AppendLine();
        text.AppendLine("Private activity (aggregate only)");
        text.AppendLine($"{report.PrivateMetrics.ActiveRepositoryCount} active repositories, {report.PrivateMetrics.CommitCount} commits, {report.PrivateMetrics.PullRequestMergedCount} merged pull requests");
        return text.ToString();
    }
}
