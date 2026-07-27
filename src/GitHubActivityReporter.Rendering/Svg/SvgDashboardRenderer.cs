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
        const string title = "Development Pulse";
        var activityState = report.HasAnyActivity ? "ACTIVE" : "QUIET";
        var updated = TimeZoneDisplay.FormatLocal(report.GeneratedAt, timeZone);
        var period = string.Create(
            CultureInfo.InvariantCulture,
            $"{TimeZoneDisplay.FormatLocalDate(report.PeriodStart, timeZone)} ~ {TimeZoneDisplay.FormatLocalDate(report.PeriodEnd, timeZone)}");
        var activeRepositories = publicRepoCount + privateMetrics.ActiveRepositoryCount;
        var mergedPullRequests = publicTotals.PullRequestMergedCount + privateMetrics.PullRequestMergedCount;

        var labels = new[] { "PUBLIC COMMITS", "PRIVATE COMMITS", "ACTIVE REPOSITORIES", "MERGED PRS" };
        var values = new[]
        {
            publicTotals.CommitCount,
            privateMetrics.CommitCount,
            activeRepositories,
            mergedPullRequests
        };

        var sb = new StringBuilder();
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" role="img" aria-labelledby="title desc" viewBox="0 0 900 330">""");
        sb.AppendLine($"  <title id=\"title\">{Escape(title)}</title>");
        sb.AppendLine($"  <desc id=\"desc\">{Escape(report.PublicNarrative.Headline ?? "Public and private development activity metrics.")}</desc>");
        sb.AppendLine("""  <defs>""");
        sb.AppendLine("""    <linearGradient id="accent" x1="0" y1="0" x2="1" y2="0">""");
        sb.AppendLine("""      <stop offset="0" stop-color="#7c3aed" />""");
        sb.AppendLine("""      <stop offset="0.55" stop-color="#2563eb" />""");
        sb.AppendLine("""      <stop offset="1" stop-color="#06b6d4" />""");
        sb.AppendLine("""    </linearGradient>""");
        sb.AppendLine("""  </defs>""");
        sb.AppendLine("""  <style>""");
        sb.AppendLine("""    text { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; }""");
        sb.AppendLine("""    .bg { fill: #ffffff; }""");
        sb.AppendLine("""    .card { fill: #f8fafc; stroke: #e2e8f0; stroke-width: 1; }""");
        sb.AppendLine("""    .title { font-size: 24px; font-weight: 750; fill: #0f172a; letter-spacing: -0.4px; }""");
        sb.AppendLine("""    .eyebrow { font-size: 10px; font-weight: 700; fill: #7c3aed; letter-spacing: 1.8px; }""");
        sb.AppendLine("""    .sub { font-size: 12px; font-weight: 500; fill: #64748b; }""");
        sb.AppendLine("""    .number { font-size: 34px; font-weight: 750; fill: #0f172a; }""");
        sb.AppendLine("""    .label { font-size: 12px; font-weight: 650; fill: #64748b; }""");
        sb.AppendLine("""    .status { font-size: 10px; font-weight: 700; fill: #047857; letter-spacing: .8px; }""");
        sb.AppendLine("""    .status-bg { fill: #d1fae5; }""");
        sb.AppendLine("""    .narrative { font-size: 13px; font-weight: 600; fill: #334155; }""");
        sb.AppendLine("""    @media (prefers-color-scheme: dark) {""");
        sb.AppendLine("""      .bg { fill: #0d1117; }""");
        sb.AppendLine("""      .card { fill: #161b22; stroke: #30363d; }""");
        sb.AppendLine("""      .title, .number { fill: #f0f6fc; }""");
        sb.AppendLine("""      .sub, .label { fill: #8b949e; }""");
        sb.AppendLine("""      .eyebrow { fill: #a78bfa; }""");
        sb.AppendLine("""      .status-bg { fill: #064e3b; }""");
        sb.AppendLine("""      .status { fill: #6ee7b7; }""");
        sb.AppendLine("""      .narrative { fill: #cbd5e1; }""");
        sb.AppendLine("""    }""");
        sb.AppendLine("""  </style>""");
        sb.AppendLine("""  <rect class="bg" x="0" y="0" width="900" height="330" rx="18" />""");
        sb.AppendLine("""  <rect x="0" y="0" width="900" height="5" rx="2.5" fill="url(#accent)" />""");
        sb.AppendLine("""  <text class="eyebrow" x="30" y="34">GITHUB ACTIVITY</text>""");
        sb.AppendLine($"  <text class=\"title\" x=\"30\" y=\"64\">{Escape(title)}</text>");
        sb.AppendLine($"  <text class=\"sub\" x=\"30\" y=\"86\">{Escape(period)}</text>");
        sb.AppendLine("""  <rect class="status-bg" x="786" y="30" width="84" height="26" rx="13" />""");
        sb.AppendLine($"  <text class=\"status\" x=\"828\" y=\"47\" text-anchor=\"middle\">{Escape(activityState)}</text>");

        for (var index = 0; index < values.Length; index++)
        {
            var x = 30 + (index * 216);
            sb.AppendLine($"  <rect class=\"card\" x=\"{x}\" y=\"112\" width=\"194\" height=\"118\" rx=\"14\" />");
            sb.AppendLine($"  <text class=\"label\" x=\"{x + 18}\" y=\"143\">{Escape(labels[index])}</text>");
            sb.AppendLine($"  <text class=\"number\" x=\"{x + 18}\" y=\"190\">{values[index].ToString(CultureInfo.InvariantCulture)}</text>");
            sb.AppendLine($"  <rect x=\"{x + 18}\" y=\"207\" width=\"42\" height=\"3\" rx=\"1.5\" fill=\"url(#accent)\" />");
        }

        if (!string.IsNullOrWhiteSpace(report.PublicNarrative.Headline))
        {
            sb.AppendLine($"  <text class=\"narrative\" x=\"30\" y=\"263\">{Escape(Truncate(report.PublicNarrative.Headline, 105))}</text>");
        }
        sb.AppendLine($"  <text class=\"sub\" x=\"30\" y=\"302\">Updated · {Escape(updated)}</text>");
        sb.AppendLine("""  <text class="sub" x="870" y="302" text-anchor="end">github-activity-reporter</text>""");
        sb.AppendLine("""</svg>""");
        return sb.ToString();
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
