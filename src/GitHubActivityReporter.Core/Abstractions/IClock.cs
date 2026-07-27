namespace GitHubActivityReporter.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Deterministic clock used by tests and by reproducible renders.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; }
}
