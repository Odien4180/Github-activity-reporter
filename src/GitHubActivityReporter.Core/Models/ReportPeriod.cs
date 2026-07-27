namespace GitHubActivityReporter.Core.Models;

/// <summary>Reporting window resolved for a run.</summary>
public sealed record ReportPeriod
{
    public required DateTimeOffset Start { get; init; }

    public required DateTimeOffset End { get; init; }

    /// <summary>True when no previous successful run was found and the fallback lookback was used.</summary>
    public bool IsInitialRun { get; init; }

    public TimeSpan Duration => End - Start;
}
