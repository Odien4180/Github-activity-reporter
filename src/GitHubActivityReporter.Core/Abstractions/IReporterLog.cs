namespace GitHubActivityReporter.Core.Abstractions;

/// <summary>Minimal logging surface. Implementations must never receive raw private data.</summary>
public interface IReporterLog
{
    void Debug(string message);

    void Info(string message);

    void Warning(string message);

    void Error(string message);
}

public sealed class NullReporterLog : IReporterLog
{
    public static readonly NullReporterLog Instance = new();

    public void Debug(string message) { }

    public void Info(string message) { }

    public void Warning(string message) { }

    public void Error(string message) { }
}

/// <summary>Collects log lines in memory. Used by tests and by the CLI summary output.</summary>
public sealed class InMemoryReporterLog : IReporterLog
{
    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public void Debug(string message) => _lines.Add("DEBUG " + message);

    public void Info(string message) => _lines.Add("INFO " + message);

    public void Warning(string message) => _lines.Add("WARN " + message);

    public void Error(string message) => _lines.Add("ERROR " + message);
}
