using System.Globalization;
using System.Text;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Formatting;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Markdown;

/// <summary>
/// Renders the compact markdown block that is injected between the profile
/// README markers. Every field is gated by the configured privacy switches.
/// </summary>
public sealed class MarkdownReportRenderer : IReportRenderer
{
    public const string DefaultTarget = "generated/activity.md";

    public string RendererId => KnownRenderers.CompactMarkdown;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var content = Render(report, context);

        var artifact = new RenderedArtifact
        {
            Name = "profile.md",
            RelativePath = string.IsNullOrWhiteSpace(context.TargetPath) ? DefaultTarget : context.TargetPath!,
            Content = content,
            Kind = RenderedArtifactKind.Markdown
        };

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts = [artifact]
        });
    }

    public string Render(ActivityReport report, RendererContext context)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        var korean = IsKorean(context);
        var privacy = context.Configuration.Privacy;
        var builder = new StringBuilder();

        builder.AppendLine(korean ? "## 최근 개발 활동" : "## Recent Development Activity");
        builder.AppendLine();
        builder.AppendLine(RenderPeriodLine(report, context, korean));
        builder.AppendLine();

        if (context.Configuration.Collection.Public.Enabled)
        {
            AppendPublicSection(builder, report, context, korean, privacy.Public);
        }

        if (context.Configuration.Collection.Private.Enabled)
        {
            AppendPrivateSection(builder, report, korean, privacy.Private);
        }

        builder.Append(korean ? "_마지막 갱신: " : "_Last updated: ");
        builder.Append(TimeZoneDisplay.FormatLocal(report.GeneratedAt, context.TimeZone));
        builder.AppendLine("_");

        return builder.ToString();
    }

    private static string RenderPeriodLine(ActivityReport report, RendererContext context, bool korean)
    {
        var start = TimeZoneDisplay.FormatLocalDate(report.PeriodStart, context.TimeZone);
        var end = TimeZoneDisplay.FormatLocalDate(report.PeriodEnd, context.TimeZone);

        return korean
            ? $"보고 기간: {start} ~ {end}"
            : $"Reporting period: {start} – {end}";
    }

    private static void AppendPublicSection(
        StringBuilder builder,
        ActivityReport report,
        RendererContext context,
        bool korean,
        PublicPrivacySettings privacy)
    {
        builder.AppendLine(korean ? "### 공개 활동" : "### Public Activity");
        builder.AppendLine();

        if (report.PublicActivities.Count == 0)
        {
            builder.AppendLine(korean
                ? "이번 기간에는 공개 저장소 활동이 없습니다."
                : "No public repository activity in this period.");
            builder.AppendLine();
            return;
        }

        var index = 0;
        foreach (var activity in report.PublicActivities)
        {
            index++;

            var heading = privacy.ExposeRepositoryNames
                ? activity.RepositoryName
                : (korean ? $"공개 저장소 #{index}" : $"Public repository #{index}");

            builder.Append("#### ").AppendLine(heading);
            builder.AppendLine();

            if (privacy.ExposeRepositoryDescriptions && !string.IsNullOrWhiteSpace(activity.Description))
            {
                builder.AppendLine(activity.Description!.Trim());
                builder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(activity.Summary))
            {
                builder.AppendLine(activity.Summary!.Trim());
                builder.AppendLine();
            }

            foreach (var line in DescribeMetrics(activity.Metrics, korean))
            {
                builder.Append("- ").AppendLine(line);
            }

            builder.AppendLine();

            AppendItems(builder, activity, privacy, korean);

            var meta = new List<string>();
            if (privacy.ExposeLanguages && !string.IsNullOrWhiteSpace(activity.Language))
            {
                meta.Add((korean ? "주요 언어: " : "Language: ") + activity.Language);
            }

            if (privacy.ExposeTopics && activity.Topics.Count > 0)
            {
                meta.Add((korean ? "토픽: " : "Topics: ") + string.Join(", ", activity.Topics));
            }

            if (meta.Count > 0)
            {
                builder.AppendLine(string.Join(" · ", meta));
                builder.AppendLine();
            }

            if (privacy.ExposeRepositoryLinks && privacy.ExposeRepositoryNames)
            {
                builder.Append('[').Append(korean ? "저장소 보기 →" : "View repository →").Append("](")
                    .Append(activity.RepositoryUrl).AppendLine(")");
                builder.AppendLine();
            }
        }
    }

    private static void AppendItems(
        StringBuilder builder,
        PublicRepositoryActivity activity,
        PublicPrivacySettings privacy,
        bool korean)
    {
        var items = new List<string>();

        foreach (var item in activity.Events)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            var allowed = item.Type switch
            {
                ActivityType.PullRequestOpened or ActivityType.PullRequestMerged or ActivityType.PullRequestClosed
                    => privacy.ExposePullRequestTitles,
                ActivityType.IssueOpened or ActivityType.IssueClosed => privacy.ExposeIssueTitles,
                ActivityType.ReleasePublished => privacy.ExposeReleaseNames,
                ActivityType.Commit => privacy.ExposeCommitMessages,
                _ => false
            };

            if (!allowed)
            {
                continue;
            }

            var label = DescribeType(item.Type, korean);
            var title = item.Title!.Trim();

            items.Add(privacy.ExposeRepositoryLinks && !string.IsNullOrWhiteSpace(item.Url)
                ? $"- {label}: [{EscapeMarkdown(title)}]({item.Url})"
                : $"- {label}: {EscapeMarkdown(title)}");
        }

        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine(item);
        }

        builder.AppendLine();
    }

    private static void AppendPrivateSection(
        StringBuilder builder,
        ActivityReport report,
        bool korean,
        PrivatePrivacySettings privacy)
    {
        builder.AppendLine(korean ? "### 비공개 활동" : "### Private Activity");
        builder.AppendLine();

        var metrics = report.PrivateMetrics;

        if (!metrics.HasActivity)
        {
            builder.AppendLine(korean
                ? "이번 기간에는 비공개 저장소 활동이 없습니다."
                : "No private repository activity in this period.");
            builder.AppendLine();
            return;
        }

        if (privacy.ExposeActiveRepositoryCount && metrics.ActiveRepositoryCount > 0)
        {
            builder.AppendLine(korean
                ? $"최근 보고 기간 동안 {metrics.ActiveRepositoryCount}개의 비공개 저장소에서 활동했습니다."
                : $"Activity happened in {metrics.ActiveRepositoryCount} private repository/repositories during this period.");
            builder.AppendLine();
        }

        var lines = new List<string>();
        AddCounter(lines, privacy.ExposeCommitCount, metrics.CommitCount, korean, "커밋", "commit", "commits");
        AddCounter(lines, privacy.ExposePullRequestOpenedCount, metrics.PullRequestOpenedCount, korean, "생성한 풀 리퀘스트", "pull request opened", "pull requests opened");
        AddCounter(lines, privacy.ExposePullRequestMergedCount, metrics.PullRequestMergedCount, korean, "병합한 풀 리퀘스트", "pull request merged", "pull requests merged");
        AddCounter(lines, privacy.ExposePullRequestClosedCount, metrics.PullRequestClosedCount, korean, "종료한 풀 리퀘스트", "pull request closed", "pull requests closed");
        AddCounter(lines, privacy.ExposeIssueOpenedCount, metrics.IssueOpenedCount, korean, "생성한 이슈", "issue opened", "issues opened");
        AddCounter(lines, privacy.ExposeIssueClosedCount, metrics.IssueClosedCount, korean, "종료한 이슈", "issue closed", "issues closed");
        AddCounter(lines, privacy.ExposeReviewCount, metrics.ReviewSubmittedCount, korean, "작성한 리뷰", "review submitted", "reviews submitted");
        AddCounter(lines, privacy.ExposeReleaseCount, metrics.ReleasePublishedCount, korean, "릴리스", "release published", "releases published");
        AddCounter(lines, privacy.ExposeActiveDayCount, metrics.ActiveDayCount, korean, "활동한 날", "active day", "active days");

        foreach (var line in lines)
        {
            builder.Append("- ").AppendLine(line);
        }

        if (lines.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static void AddCounter(
        List<string> lines,
        bool enabled,
        int value,
        bool korean,
        string koreanLabel,
        string englishSingular,
        string englishPlural)
    {
        if (!enabled || value <= 0)
        {
            return;
        }

        var number = value.ToString(CultureInfo.InvariantCulture);
        lines.Add(korean
            ? $"{koreanLabel} {number}건"
            : $"{number} {(value == 1 ? englishSingular : englishPlural)}");
    }

    private static IEnumerable<string> DescribeMetrics(PublicActivityMetrics metrics, bool korean)
    {
        var lines = new List<string>();
        AddCounter(lines, true, metrics.CommitCount, korean, "커밋", "commit", "commits");
        AddCounter(lines, true, metrics.PullRequestOpenedCount, korean, "생성한 풀 리퀘스트", "pull request opened", "pull requests opened");
        AddCounter(lines, true, metrics.PullRequestMergedCount, korean, "병합한 풀 리퀘스트", "pull request merged", "pull requests merged");
        AddCounter(lines, true, metrics.PullRequestClosedCount, korean, "종료한 풀 리퀘스트", "pull request closed", "pull requests closed");
        AddCounter(lines, true, metrics.IssueOpenedCount, korean, "생성한 이슈", "issue opened", "issues opened");
        AddCounter(lines, true, metrics.IssueClosedCount, korean, "종료한 이슈", "issue closed", "issues closed");
        AddCounter(lines, true, metrics.ReviewSubmittedCount, korean, "작성한 리뷰", "review submitted", "reviews submitted");
        AddCounter(lines, true, metrics.ReleasePublishedCount, korean, "릴리스", "release published", "releases published");
        return lines;
    }

    private static string DescribeType(ActivityType type, bool korean) => type switch
    {
        ActivityType.PullRequestOpened => korean ? "PR 생성" : "PR opened",
        ActivityType.PullRequestMerged => korean ? "PR 병합" : "PR merged",
        ActivityType.PullRequestClosed => korean ? "PR 종료" : "PR closed",
        ActivityType.IssueOpened => korean ? "이슈 생성" : "Issue opened",
        ActivityType.IssueClosed => korean ? "이슈 종료" : "Issue closed",
        ActivityType.ReviewSubmitted => korean ? "리뷰" : "Review",
        ActivityType.ReleasePublished => korean ? "릴리스" : "Release",
        ActivityType.Commit => korean ? "커밋" : "Commit",
        _ => type.ToString()
    };

    private static string EscapeMarkdown(string value)
        => value.Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

    private static bool IsKorean(RendererContext context)
        => string.Equals(context.Language, "ko", StringComparison.OrdinalIgnoreCase);
}
