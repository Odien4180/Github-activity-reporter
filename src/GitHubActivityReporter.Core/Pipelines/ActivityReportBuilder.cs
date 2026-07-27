using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Pipelines;

public sealed record ReportBuildContext
{
    public required string GitHubUserName { get; init; }

    public required ReportPeriod Period { get; init; }
}

/// <summary>
/// Composes the final <see cref="ActivityReport"/>: public events are summarised,
/// private events are aggregated into anonymous counters and then discarded.
/// </summary>
public sealed class ActivityReportBuilder
{
    private readonly IPublicActivitySummarizer _summarizer;
    private readonly IClock _clock;
    private readonly IPrivateActivityAggregator _aggregator;

    public ActivityReportBuilder(IPublicActivitySummarizer summarizer, IClock clock)
        : this(summarizer, clock, new PrivateActivityAggregator())
    {
    }

    internal ActivityReportBuilder(
        IPublicActivitySummarizer summarizer,
        IClock clock,
        IPrivateActivityAggregator aggregator)
    {
        _summarizer = summarizer ?? throw new ArgumentNullException(nameof(summarizer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
    }

    public async Task<ActivityReport> BuildAsync(
        CollectedActivity collected,
        ReportBuildContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collected);
        ArgumentNullException.ThrowIfNull(context);

        // Only public events are handed to a summarizer, never private ones.
        var publicActivities = await _summarizer
            .SummarizeAsync(collected.PublicEvents, cancellationToken)
            .ConfigureAwait(false);

        var privateMetrics = _aggregator.Aggregate(collected.PrivateEvents);

        return new ActivityReport
        {
            GeneratedAt = _clock.UtcNow,
            PeriodStart = context.Period.Start,
            PeriodEnd = context.Period.End,
            GitHubUserName = context.GitHubUserName,
            PublicActivities = publicActivities,
            PrivateMetrics = privateMetrics
        };
    }
}
