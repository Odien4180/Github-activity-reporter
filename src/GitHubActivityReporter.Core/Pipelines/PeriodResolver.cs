using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.State;

namespace GitHubActivityReporter.Core.Pipelines;

/// <summary>Resolves the reporting window for a run.</summary>
public sealed class PeriodResolver
{
    private readonly IClock _clock;

    public PeriodResolver(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ReportPeriod Resolve(ReporterConfiguration configuration, ReporterState? state)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var now = _clock.UtcNow;
        var settings = configuration.Collection.Period;
        var initialLookback = DurationParser.ParseOrDefault(settings.InitialLookback, TimeSpan.FromHours(24));

        switch (settings.Mode)
        {
            case PeriodMode.Last24Hours:
                return new ReportPeriod { Start = now - TimeSpan.FromHours(24), End = now };

            case PeriodMode.Last7Days:
                return new ReportPeriod { Start = now - TimeSpan.FromDays(7), End = now };

            case PeriodMode.Custom:
                var custom = DurationParser.ParseOrDefault(settings.CustomLookback, initialLookback);
                return new ReportPeriod { Start = now - custom, End = now };

            case PeriodMode.SinceLastSuccess:
            default:
                if (state?.LastSuccessfulRunAt is { } lastSuccess && lastSuccess <= now)
                {
                    return new ReportPeriod { Start = lastSuccess, End = now };
                }

                return new ReportPeriod
                {
                    Start = now - initialLookback,
                    End = now,
                    IsInitialRun = true
                };
        }
    }
}
