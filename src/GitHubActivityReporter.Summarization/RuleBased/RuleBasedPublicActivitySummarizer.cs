using System.Globalization;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;

namespace GitHubActivityReporter.Summarization.RuleBased;

/// <summary>
/// Deterministic summarizer. It only restates facts that are present in the
/// collected public events, never invents work and never touches private data.
/// </summary>
public sealed class RuleBasedPublicActivitySummarizer : IPublicActivitySummarizer
{
    private readonly SummarySettings _settings;

    public RuleBasedPublicActivitySummarizer(SummarySettings? settings = null)
    {
        _settings = settings ?? new SummarySettings();
    }

    public Task<PublicActivitySummary> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        cancellationToken.ThrowIfCancellationRequested();

        var activities = events
            .GroupBy(e => e.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .Select(BuildRepositoryActivity)
            .OrderByDescending(a => a.Metrics.TotalCount)
            .ThenByDescending(a => a.Events.Count == 0 ? DateTimeOffset.MinValue : a.Events.Max(e => e.OccurredAt))
            .ThenBy(a => a.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, _settings.MaxPublicRepositories))
            .ToArray();

        return Task.FromResult(new PublicActivitySummary
        {
            Repositories = activities,
            Narrative = BuildNarrative(activities)
        });
    }

    private PublicRepositoryActivity BuildRepositoryActivity(IGrouping<string, PublicActivityEvent> group)
    {
        var all = group.OrderByDescending(e => e.OccurredAt).ToArray();
        var metrics = PublicActivityMetricsCalculator.Calculate(all);

        var notable = all
            .Where(e => e.Type != ActivityType.Commit && !string.IsNullOrWhiteSpace(e.Title))
            .Take(Math.Max(1, _settings.MaxItemsPerRepository))
            .ToArray();

        var displayed = notable.Length > 0
            ? notable
            : all.Take(Math.Max(1, _settings.MaxItemsPerRepository)).ToArray();

        return new PublicRepositoryActivity
        {
            RepositoryName = group.Key,
            RepositoryUrl = all.Select(e => e.RepositoryUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
                            ?? $"https://github.com/{group.Key}",
            Description = all.Select(e => e.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
            Language = all.Select(e => e.Language).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)),
            Topics = all.SelectMany(e => e.Topics).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Events = displayed,
            Metrics = metrics,
            Summary = BuildSummary(notable, metrics, all.Select(e => e.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)))
        };
    }

    private string BuildSummary(
        IReadOnlyList<PublicActivityEvent> notable,
        PublicActivityMetrics metrics,
        string? repositoryDescription)
    {
        var korean = string.Equals(_settings.Language, "ko", StringComparison.OrdinalIgnoreCase);
        if (_settings.UsePublicChangeDetails)
        {
            var detailSummary = BuildChangeDetailSummary(notable.Count > 0 ? notable : Array.Empty<PublicActivityEvent>(), korean);
            if (!string.IsNullOrWhiteSpace(detailSummary))
            {
                return detailSummary!;
            }
        }

        if (notable.Count > 0)
        {
            var titles = notable
                .Select(item => item.Title!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, _settings.MaxItemsPerRepository))
                .ToArray();
            var describedWork = string.Join(korean ? ", " : "; ", titles.Select(title => $"\"{title}\""));

            return korean
                ? $"{describedWork} 중심으로 공개 작업 흐름을 다듬었습니다."
                : $"Refined the public delivery flow around {describedWork}.";
        }

        if (metrics.CommitCount > 0)
        {
            if (!string.IsNullOrWhiteSpace(repositoryDescription))
            {
                return korean
                    ? $"{repositoryDescription.Trim()} 방향으로 구현과 정비를 이어갔습니다. (커밋 {metrics.CommitCount.ToString(CultureInfo.InvariantCulture)}건)"
                    : $"Kept implementation moving in line with {repositoryDescription.Trim()}. ({metrics.CommitCount.ToString(CultureInfo.InvariantCulture)} commits)";
            }

            return korean
                ? $"커밋 {metrics.CommitCount.ToString(CultureInfo.InvariantCulture)}건으로 공개 변경 사항을 정리했습니다."
                : $"Wrapped up the public-facing changes in {metrics.CommitCount.ToString(CultureInfo.InvariantCulture)} commit(s).";
        }

        return korean
            ? $"총 {metrics.TotalCount.ToString(CultureInfo.InvariantCulture)}건의 공개 활동을 정리했습니다."
            : $"Captured {metrics.TotalCount.ToString(CultureInfo.InvariantCulture)} public activity item(s).";
    }

    private string? BuildChangeDetailSummary(
        IReadOnlyList<PublicActivityEvent> events,
        bool korean)
    {
        var paths = events
            .SelectMany(e => e.ChangedPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Take(20)
            .ToArray();
        var theme = ClassifyTheme(paths);
        if (theme is null)
        {
            return null;
        }

        var additions = events.Sum(e => e.Additions ?? 0);
        var deletions = events.Sum(e => e.Deletions ?? 0);
        var files = events.Sum(e => e.ChangedFiles ?? 0);
        var level = _settings.PublicChangeDetailLevel;

        return (theme, korean, level) switch
        {
            ("configuration", true, "detailed") => $"설정 흐름을 정비하고 관련 변경 파일 {files}개를 조정했습니다. (+{additions}/-{deletions})",
            ("configuration", true, _) => "설정 흐름과 연결 지점을 정비했습니다.",
            ("reliability", true, "detailed") => $"오류 처리와 안정성 보강에 집중했습니다. 변경 파일 {files}개, +{additions}/-{deletions}입니다.",
            ("reliability", true, _) => "오류 처리와 안정성 보강을 진행했습니다.",
            ("documentation", true, "detailed") => $"문서와 사용 흐름 설명을 다듬었습니다. 관련 파일 {files}개를 조정했습니다.",
            ("documentation", true, _) => "문서와 사용 흐름 설명을 다듬었습니다.",
            ("automation", true, "detailed") => $"자동화와 워크플로 구성을 손봤습니다. 변경 파일 {files}개를 정리했습니다.",
            ("automation", true, _) => "자동화와 워크플로 구성을 손봤습니다.",
            ("configuration", false, "detailed") => $"Refined the configuration flow across {files} changed file(s). (+{additions}/-{deletions})",
            ("configuration", false, _) => "Refined the configuration flow and its integration points.",
            ("reliability", false, "detailed") => $"Focused on error handling and reliability updates across {files} changed file(s). (+{additions}/-{deletions})",
            ("reliability", false, _) => "Strengthened error handling and reliability.",
            ("documentation", false, "detailed") => $"Updated documentation and usage guidance across {files} changed file(s).",
            ("documentation", false, _) => "Updated documentation and usage guidance.",
            ("automation", false, "detailed") => $"Adjusted automation and workflow setup across {files} changed file(s).",
            ("automation", false, _) => "Adjusted automation and workflow setup.",
            _ => null
        };
    }

    private static string? ClassifyTheme(IReadOnlyList<string> paths)
    {
        if (paths.Any(path => path.Contains("workflow", StringComparison.OrdinalIgnoreCase)
                              || path.Contains(".github/", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("actions", StringComparison.OrdinalIgnoreCase)))
        {
            return "automation";
        }

        if (paths.Any(path => path.Contains("readme", StringComparison.OrdinalIgnoreCase)
                              || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("docs", StringComparison.OrdinalIgnoreCase)))
        {
            return "documentation";
        }

        if (paths.Any(path => path.Contains("config", StringComparison.OrdinalIgnoreCase)
                              || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                              || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("settings", StringComparison.OrdinalIgnoreCase)))
        {
            return "configuration";
        }

        if (paths.Any(path => path.Contains("test", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("retry", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("error", StringComparison.OrdinalIgnoreCase)
                              || path.Contains("validation", StringComparison.OrdinalIgnoreCase)))
        {
            return "reliability";
        }

        return null;
    }

    private PublicActivityNarrative BuildNarrative(IReadOnlyList<PublicRepositoryActivity> activities)
    {
        if (activities.Count == 0)
        {
            return new PublicActivityNarrative();
        }

        var korean = string.Equals(_settings.Language, "ko", StringComparison.OrdinalIgnoreCase);
        var totalCommits = activities.Sum(a => a.Metrics.CommitCount);
        var totalPullRequests = activities.Sum(a =>
            a.Metrics.PullRequestOpenedCount + a.Metrics.PullRequestMergedCount + a.Metrics.PullRequestClosedCount);
        var totalIssues = activities.Sum(a => a.Metrics.IssueOpenedCount + a.Metrics.IssueClosedCount);
        var totalReviews = activities.Sum(a => a.Metrics.ReviewSubmittedCount);
        var totalReleases = activities.Sum(a => a.Metrics.ReleasePublishedCount);

        var headline = korean
            ? BuildKoreanHeadline(activities.Count, totalCommits, totalPullRequests, totalIssues, totalReviews, totalReleases)
            : BuildEnglishHeadline(activities.Count, totalCommits, totalPullRequests, totalIssues, totalReviews, totalReleases);

        var highlights = activities
            .Take(5)
            .Select(activity => BuildRepositoryHighlight(activity, korean))
            .ToArray();

        return new PublicActivityNarrative { Headline = headline, Highlights = highlights };
    }

    private static string BuildKoreanHeadline(
        int repositoryCount,
        int commits,
        int pullRequests,
        int issues,
        int reviews,
        int releases)
    {
        var themes = new List<string>();
        if (pullRequests > 0) themes.Add("변경 사항 검토와 병합");
        if (issues > 0) themes.Add("이슈 대응");
        if (reviews > 0) themes.Add("코드 리뷰");
        if (releases > 0) themes.Add("릴리스");

        return themes.Count > 0
            ? $"{repositoryCount}개 공개 저장소에서 {string.Join(", ", themes)}를 중심으로 작업 흐름을 정리했습니다."
            : $"{repositoryCount}개 공개 저장소에 {commits}개의 커밋을 반영하며 공개 작업을 이어갔습니다.";
    }

    private static string BuildEnglishHeadline(
        int repositoryCount,
        int commits,
        int pullRequests,
        int issues,
        int reviews,
        int releases)
    {
        var themes = new List<string>();
        if (pullRequests > 0) themes.Add("pull request delivery");
        if (issues > 0) themes.Add("issue resolution");
        if (reviews > 0) themes.Add("code review");
        if (releases > 0) themes.Add("releases");

        return themes.Count > 0
            ? $"Worked across {repositoryCount} public repositories with a focus on {string.Join(", ", themes)}."
            : $"Kept public work moving across {repositoryCount} repositories with {commits} commits.";
    }

    private static string BuildRepositoryHighlight(PublicRepositoryActivity activity, bool korean)
    {
        var metrics = activity.Metrics;
        var parts = new List<string>();
        if (metrics.CommitCount > 0) parts.Add(korean ? $"커밋 {metrics.CommitCount}건" : $"{metrics.CommitCount} commits");
        if (metrics.PullRequestMergedCount > 0) parts.Add(korean ? $"PR 병합 {metrics.PullRequestMergedCount}건" : $"{metrics.PullRequestMergedCount} merged PRs");
        if (metrics.PullRequestOpenedCount > 0) parts.Add(korean ? $"PR 생성 {metrics.PullRequestOpenedCount}건" : $"{metrics.PullRequestOpenedCount} opened PRs");
        if (metrics.IssueClosedCount > 0) parts.Add(korean ? $"이슈 해결 {metrics.IssueClosedCount}건" : $"{metrics.IssueClosedCount} closed issues");
        if (metrics.ReviewSubmittedCount > 0) parts.Add(korean ? $"리뷰 {metrics.ReviewSubmittedCount}건" : $"{metrics.ReviewSubmittedCount} reviews");
        if (metrics.ReleasePublishedCount > 0) parts.Add(korean ? $"릴리스 {metrics.ReleasePublishedCount}건" : $"{metrics.ReleasePublishedCount} releases");

        var language = string.IsNullOrWhiteSpace(activity.Language)
            ? string.Empty
            : korean ? $" ({activity.Language})" : $" ({activity.Language})";
        return korean
            ? $"{activity.RepositoryName}{language}: {string.Join(", ", parts)}을 중심으로 흐름을 정리했습니다."
            : $"{activity.RepositoryName}{language}: {string.Join(", ", parts)}.";
    }
}
