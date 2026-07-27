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

    public Task<IReadOnlyList<PublicRepositoryActivity>> SummarizeAsync(
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

        return Task.FromResult<IReadOnlyList<PublicRepositoryActivity>>(activities);
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
}
