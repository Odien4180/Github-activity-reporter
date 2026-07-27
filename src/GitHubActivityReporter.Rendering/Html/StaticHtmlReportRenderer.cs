using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Formatting;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Html;

/// <summary>
/// Builds a static website bundle (HTML/CSS/JS + data JSON) from one report.
/// </summary>
public sealed class StaticHtmlReportRenderer : IReportRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string RendererId => KnownRenderers.StaticHtml;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var root = ResolveRoot(context.TargetPath, context.Configuration.Outputs.Website.OutputDirectory);
        var latestJson = BuildLatestJson(report, context);
        var historyJson = BuildHistoryJson(report, context);

        var artifacts = new List<RenderedArtifact>
        {
            new()
            {
                Name = "index.html",
                RelativePath = Combine(root, "index.html"),
                Content = BuildHtml(report, context),
                Kind = RenderedArtifactKind.Html
            },
            new()
            {
                Name = "style.css",
                RelativePath = Combine(root, "assets/style.css"),
                Content = BuildCss(),
                Kind = RenderedArtifactKind.Css
            },
            new()
            {
                Name = "app.js",
                RelativePath = Combine(root, "assets/app.js"),
                Content = BuildJavaScript(),
                Kind = RenderedArtifactKind.JavaScript
            },
            new()
            {
                Name = "latest.json",
                RelativePath = Combine(root, "data/latest.json"),
                Content = latestJson,
                Kind = RenderedArtifactKind.Json
            },
            new()
            {
                Name = "history.json",
                RelativePath = Combine(root, "data/history.json"),
                Content = historyJson,
                Kind = RenderedArtifactKind.Json
            }
        };

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts = artifacts
        });
    }

    private static string ResolveRoot(string? targetPath, string configuredOutputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var normalized = targetPath!.Replace('\\', '/');
            if (normalized.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
            {
                return normalized[..^"/index.html".Length];
            }

            if (normalized.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(normalized)!.Replace('\\', '/');
            }

            return normalized.TrimEnd('/');
        }

        return configuredOutputDirectory.Replace('\\', '/').TrimEnd('/');
    }

    private static string BuildHtml(ActivityReport report, RendererContext context)
    {
        var tz = context.TimeZone;
        var periodStart = TimeZoneDisplay.FormatLocalDate(report.PeriodStart, tz);
        var periodEnd = TimeZoneDisplay.FormatLocalDate(report.PeriodEnd, tz);
        var updated = TimeZoneDisplay.FormatLocal(report.GeneratedAt, tz);
        var publicTotals = report.PublicTotals;
        var privateMetrics = report.PrivateMetrics;

        var sb = new StringBuilder();
        sb.AppendLine("""<!doctype html>""");
        sb.AppendLine("""<html lang="en">""");
        sb.AppendLine("""<head>""");
        sb.AppendLine("""  <meta charset="utf-8">""");
        sb.AppendLine("""  <meta name="viewport" content="width=device-width, initial-scale=1">""");
        sb.AppendLine("""  <title>GitHub Activity Report</title>""");
        sb.AppendLine("""  <link rel="stylesheet" href="./assets/style.css">""");
        sb.AppendLine("""</head>""");
        sb.AppendLine("""<body>""");
        sb.AppendLine("""  <main class="wrap">""");
        sb.AppendLine("""    <h1>GitHub Activity Report</h1>""");
        sb.AppendLine($"    <p class=\"meta\"><strong>User:</strong> {Escape(report.GitHubUserName)}</p>");
        sb.AppendLine($"    <p class=\"meta\"><strong>Period:</strong> {Escape(periodStart)} ~ {Escape(periodEnd)}</p>");
        sb.AppendLine($"    <p class=\"meta\"><strong>Updated:</strong> {Escape(updated)}</p>");
        sb.AppendLine("""    <section class="card">""");
        sb.AppendLine("""      <h2>Public activity summary</h2>""");
        if (!string.IsNullOrWhiteSpace(report.PublicNarrative.Headline))
        {
            sb.AppendLine($"      <p class=\"headline\">{Escape(report.PublicNarrative.Headline)}</p>");
            sb.AppendLine("      <ul class=\"highlights\">");
            foreach (var highlight in report.PublicNarrative.Highlights)
            {
                sb.AppendLine($"        <li>{Escape(highlight)}</li>");
            }
            sb.AppendLine("      </ul>");
        }
        sb.AppendLine($"      <ul><li>{publicTotals.CommitCount} commits</li><li>{publicTotals.PullRequestMergedCount} merged pull requests</li><li>{publicTotals.IssueClosedCount} closed issues</li></ul>");
        sb.AppendLine("""    </section>""");
        sb.AppendLine("""    <section class="card">""");
        sb.AppendLine("""      <h2>Public repositories</h2>""");
        if (report.PublicActivities.Count == 0)
        {
            sb.AppendLine("""      <p>No public repository activity in this period.</p>""");
        }
        else
        {
            foreach (var repo in report.PublicActivities)
            {
                sb.AppendLine("""      <article class="repo">""");
                sb.AppendLine($"        <h3>{Escape(repo.RepositoryName)}</h3>");
                sb.AppendLine($"        <p><a href=\"{Escape(repo.RepositoryUrl)}\" rel=\"noopener noreferrer\">{Escape(repo.RepositoryUrl)}</a></p>");
                if (!string.IsNullOrWhiteSpace(repo.Summary))
                {
                    sb.AppendLine($"        <p>{Escape(repo.Summary!)}</p>");
                }

                sb.AppendLine($"        <p>{repo.Metrics.CommitCount} commits · {repo.Metrics.PullRequestMergedCount} merged PRs · {repo.Metrics.IssueClosedCount} closed issues</p>");
                sb.AppendLine("""      </article>""");
            }
        }

        sb.AppendLine("""    </section>""");
        sb.AppendLine("""    <section class="card">""");
        sb.AppendLine("""      <h2>Private activity metrics</h2>""");
        sb.AppendLine($"      <ul><li>{privateMetrics.ActiveRepositoryCount} active private repositories</li><li>{privateMetrics.CommitCount} commits</li><li>{privateMetrics.PullRequestOpenedCount} pull requests opened</li><li>{privateMetrics.PullRequestMergedCount} pull requests merged</li><li>{privateMetrics.IssueClosedCount} issues closed</li><li>{privateMetrics.ReviewSubmittedCount} reviews submitted</li><li>{privateMetrics.ActiveDayCount} active days</li></ul>");
        sb.AppendLine("""    </section>""");
        sb.AppendLine("""    <section class="card">""");
        sb.AppendLine("""      <h2>Activity trend</h2>""");
        sb.AppendLine("""      <p>See <code>data/history.json</code> for daily aggregate counters.</p>""");
        sb.AppendLine("""    </section>""");
        sb.AppendLine("""  </main>""");
        sb.AppendLine("""  <script src="./assets/app.js"></script>""");
        sb.AppendLine("""</body>""");
        sb.AppendLine("""</html>""");
        return sb.ToString();
    }

    private static string BuildCss() =>
        """
        :root {
          color-scheme: light dark;
          --bg: #ffffff;
          --fg: #24292f;
          --muted: #57606a;
          --card: #f6f8fa;
          --border: #d0d7de;
        }
        @media (prefers-color-scheme: dark) {
          :root {
            --bg: #0d1117;
            --fg: #e6edf3;
            --muted: #8b949e;
            --card: #161b22;
            --border: #30363d;
          }
        }
        body {
          margin: 0;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
          background: var(--bg);
          color: var(--fg);
        }
        .wrap {
          max-width: 920px;
          margin: 0 auto;
          padding: 24px 16px 40px;
        }
        .meta {
          color: var(--muted);
          margin: 4px 0;
        }
        .headline {
          font-size: 1.08rem;
          font-weight: 650;
          line-height: 1.55;
        }
        .highlights {
          line-height: 1.65;
        }
        .card {
          border: 1px solid var(--border);
          background: var(--card);
          border-radius: 10px;
          padding: 14px 16px;
          margin-top: 14px;
        }
        .repo {
          border-top: 1px solid var(--border);
          padding-top: 10px;
          margin-top: 10px;
        }
        .repo:first-of-type {
          border-top: none;
          padding-top: 0;
          margin-top: 0;
        }
        a {
          color: inherit;
        }
        """;

    private static string BuildJavaScript() =>
        """
        (() => {
          const endpoint = './data/latest.json';
          fetch(endpoint).then(() => {
            // Static site intentionally keeps runtime logic minimal.
          }).catch(() => {
            // Ignore network failures; page content is already usable.
          });
        })();
        """;

    private static string BuildLatestJson(ActivityReport report, RendererContext context)
    {
        var payload = new
        {
            generatedAt = report.GeneratedAt,
            periodStart = report.PeriodStart,
            periodEnd = report.PeriodEnd,
            user = report.GitHubUserName,
            publicActivity = new
            {
                activeRepositoryCount = report.PublicActivities.Count,
                metrics = report.PublicTotals,
                narrative = report.PublicNarrative
            },
            privateActivity = report.PrivateMetrics,
            timeZone = context.TimeZone.Id
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildHistoryJson(ActivityReport report, RendererContext context)
    {
        var days = Math.Max(1, context.Configuration.Outputs.Website.HistoryDays);
        var endDate = TimeZoneInfo.ConvertTime(report.PeriodEnd, context.TimeZone).Date;
        var points = new List<object>(days);

        for (var i = days - 1; i >= 0; i--)
        {
            var day = endDate.AddDays(-i);
            points.Add(new
            {
                date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                publicCommits = 0,
                publicMergedPullRequests = 0,
                publicClosedIssues = 0,
                privateCommits = 0,
                privateMergedPullRequests = 0,
                privateClosedIssues = 0
            });
        }

        var payload = new
        {
            generatedAt = report.GeneratedAt,
            historyDays = days,
            points
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string Combine(string root, string path)
        => $"{root.TrimEnd('/')}/{path}".Replace('\\', '/');

    private static string Escape(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
