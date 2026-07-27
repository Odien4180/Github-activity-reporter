using System.Globalization;
using System.Text;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Formatting;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Svg;

/// <summary>
/// Generates an embeddable SVG activity card suitable for GitHub README pages.
/// </summary>
public sealed class SvgDashboardRenderer : IReportRenderer
{
    public const string DefaultTarget = "generated/activity-dashboard.svg";

    public string RendererId => KnownRenderers.SvgDashboard;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var content = Render(report, context);
        var target = string.IsNullOrWhiteSpace(context.TargetPath) ? DefaultTarget : context.TargetPath!;

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts =
            [
                new RenderedArtifact
                {
                    Name = "activity-dashboard.svg",
                    RelativePath = target,
                    Content = content,
                    Kind = RenderedArtifactKind.Svg
                }
            ]
        });
    }

    public string Render(ActivityReport report, RendererContext context)
    {
        var timeZone = context.TimeZone;
        var publicTotals = report.PublicTotals;
        var privateMetrics = report.PrivateMetrics;
        var publicRepoCount = report.PublicActivities.Count;
        var privateIssueAndPr = privateMetrics.IssueClosedCount + privateMetrics.PullRequestMergedCount;
        var publicIssueAndPr = publicTotals.IssueClosedCount + publicTotals.PullRequestMergedCount;
        var activityState = report.HasAnyActivity ? "Active" : "Quiet";
        var updated = TimeZoneDisplay.FormatLocal(report.GeneratedAt, timeZone);
        var period = string.Create(
            CultureInfo.InvariantCulture,
            $"{TimeZoneDisplay.FormatLocalDate(report.PeriodStart, timeZone)} ~ {TimeZoneDisplay.FormatLocalDate(report.PeriodEnd, timeZone)}");

        var sb = new StringBuilder();
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" role="img" aria-labelledby="title desc" viewBox="0 0 860 280">""");
        sb.AppendLine("""  <title id="title">Development Activity Dashboard</title>""");
        sb.AppendLine("""  <desc id="desc">Public and private repository activity counters.</desc>""");
        sb.AppendLine("""  <style>""");
        sb.AppendLine("""    .bg { fill: #ffffff; }""");
        sb.AppendLine("""    .panel { fill: #f6f8fa; stroke: #d0d7de; stroke-width: 1; }""");
        sb.AppendLine("""    .title { font: 700 24px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; fill: #24292f; }""");
        sb.AppendLine("""    .sub { font: 500 14px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; fill: #57606a; }""");
        sb.AppendLine("""    .h { font: 700 18px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; fill: #24292f; }""");
        sb.AppendLine("""    .line { font: 500 14px -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; fill: #24292f; }""");
        sb.AppendLine("""    @media (prefers-color-scheme: dark) {""");
        sb.AppendLine("""      .bg { fill: #0d1117; }""");
        sb.AppendLine("""      .panel { fill: #161b22; stroke: #30363d; }""");
        sb.AppendLine("""      .title, .h, .line { fill: #e6edf3; }""");
        sb.AppendLine("""      .sub { fill: #8b949e; }""");
        sb.AppendLine("""    }""");
        sb.AppendLine("""  </style>""");
        sb.AppendLine("""  <rect class="bg" x="0" y="0" width="860" height="280" rx="12" />""");
        sb.AppendLine("""  <text class="title" x="28" y="40">Development Activity</text>""");
        sb.AppendLine($"  <text class=\"sub\" x=\"28\" y=\"64\">Period: {Escape(period)}</text>");
        sb.AppendLine($"  <text class=\"sub\" x=\"650\" y=\"64\">Status: {activityState}</text>");
        sb.AppendLine("""  <rect class="panel" x="24" y="84" width="392" height="156" rx="10" />""");
        sb.AppendLine("""  <text class="h" x="42" y="112">Public</text>""");
        sb.AppendLine($"  <text class=\"line\" x=\"42\" y=\"138\">{publicRepoCount} active repositories</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"42\" y=\"160\">{publicTotals.CommitCount} commits</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"42\" y=\"182\">{publicTotals.PullRequestMergedCount} merged PRs</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"42\" y=\"204\">{publicIssueAndPr} merged PRs + closed issues</text>");
        sb.AppendLine("""  <rect class="panel" x="444" y="84" width="392" height="156" rx="10" />""");
        sb.AppendLine("""  <text class="h" x="462" y="112">Private</text>""");
        sb.AppendLine($"  <text class=\"line\" x=\"462\" y=\"138\">{privateMetrics.ActiveRepositoryCount} active repositories</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"462\" y=\"160\">{privateMetrics.CommitCount} commits</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"462\" y=\"182\">{privateMetrics.PullRequestMergedCount} merged PRs</text>");
        sb.AppendLine($"  <text class=\"line\" x=\"462\" y=\"204\">{privateIssueAndPr} merged PRs + closed issues</text>");
        sb.AppendLine($"  <text class=\"sub\" x=\"28\" y=\"264\">Updated {Escape(updated)}</text>");
        sb.AppendLine("""</svg>""");
        return sb.ToString();
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
