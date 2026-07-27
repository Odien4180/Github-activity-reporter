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
            Summary = BuildSummary(notable, metrics)
        };
    }

    private string BuildSummary(IReadOnlyList<PublicActivityEvent> notable, PublicActivityMetrics metrics)
    {
        var korean = string.Equals(_settings.Language, "ko", StringComparison.OrdinalIgnoreCase);

        if (notable.Count > 0)
        {
            var headline = notable[0].Title!.Trim();
            var others = metrics.TotalCount - 1;

            if (others <= 0)
            {
                return korean
                    ? $"\"{headline}\" 작업을 진행했습니다."
                    : $"Worked on \"{headline}\".";
            }

            return korean
                ? $"\"{headline}\" 외 {others.ToString(CultureInfo.InvariantCulture)}건의 작업을 진행했습니다."
                : $"Worked on \"{headline}\" and {others.ToString(CultureInfo.InvariantCulture)} other item(s).";
        }

        if (metrics.CommitCount > 0)
        {
            return korean
                ? $"커밋 {metrics.CommitCount.ToString(CultureInfo.InvariantCulture)}건을 반영했습니다."
                : $"Pushed {metrics.CommitCount.ToString(CultureInfo.InvariantCulture)} commit(s).";
        }

        return korean
            ? $"총 {metrics.TotalCount.ToString(CultureInfo.InvariantCulture)}건의 활동이 있었습니다."
            : $"{metrics.TotalCount.ToString(CultureInfo.InvariantCulture)} activity item(s) recorded.";
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
            ? $"{repositoryCount}개 공개 저장소에서 {string.Join(", ", themes)}를 중심으로 개발 활동을 진행했습니다."
            : $"{repositoryCount}개 공개 저장소에 {commits}개의 커밋을 반영하며 개발을 이어갔습니다.";
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
            : $"Continued development across {repositoryCount} public repositories with {commits} commits.";
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
            ? $"{activity.RepositoryName}{language}: {string.Join(", ", parts)}을 진행했습니다."
            : $"{activity.RepositoryName}{language}: {string.Join(", ", parts)}.";
    }
}
