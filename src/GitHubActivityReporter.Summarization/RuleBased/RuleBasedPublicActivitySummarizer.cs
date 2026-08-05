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
            Summary = BuildSummary(all, notable)
        };
    }

    private string BuildSummary(
        IReadOnlyList<PublicActivityEvent> all,
        IReadOnlyList<PublicActivityEvent> notable)
    {
        var korean = string.Equals(_settings.Language, "ko", StringComparison.OrdinalIgnoreCase);
        if (_settings.UsePublicChangeDetails)
        {
            var detailSummary = BuildChangeDetailSummary(all, korean);
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

        if (all.Any(e => e.Type == ActivityType.Commit))
        {
            return korean
                ? "공개 변경 내용을 구현하고 관련 흐름을 정비했습니다."
                : "Implemented the public changes and refined the related workflow.";
        }

        return korean
            ? "공개 프로젝트의 작업 흐름을 정리했습니다."
            : "Refined the public project's development workflow.";
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
        var theme = ClassifyTheme(paths) ?? ClassifyTextTheme(events);
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

    private static string? ClassifyTextTheme(IReadOnlyList<PublicActivityEvent> events)
    {
        var text = string.Join(
            ' ',
            events.Select(item => item.Title).Where(title => !string.IsNullOrWhiteSpace(title)));

        if (ContainsAny(text, "workflow", "action", "automation", "pipeline", "deploy", "release"))
        {
            return "automation";
        }

        if (ContainsAny(text, "readme", "documentation", "docs", "guide"))
        {
            return "documentation";
        }

        if (ContainsAny(text, "config", "configuration", "setting", "setup"))
        {
            return "configuration";
        }

        if (ContainsAny(text, "test", "retry", "error", "reliable", "reliability", "timeout", "validation", "fix"))
        {
            return "reliability";
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] terms)
        => terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private PublicActivityNarrative BuildNarrative(IReadOnlyList<PublicRepositoryActivity> activities)
    {
        if (activities.Count == 0)
        {
            return new PublicActivityNarrative();
        }

        var korean = string.Equals(_settings.Language, "ko", StringComparison.OrdinalIgnoreCase);
        var highlights = activities
            .Take(5)
            .Where(activity => !string.IsNullOrWhiteSpace(activity.Summary))
            .Select(activity => $"{activity.RepositoryName}: {activity.Summary}")
            .ToArray();
        var primarySummary = activities
            .Select(activity => activity.Summary)
            .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary));
        var headline = primarySummary is null
            ? null
            : korean
                ? $"이번 기간의 주요 작업: {primarySummary}"
                : $"Primary work this period: {primarySummary}";

        return new PublicActivityNarrative { Headline = headline, Highlights = highlights };
    }
}
