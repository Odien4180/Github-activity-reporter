using System.Text.Json;
using GitHubActivityReporter.Core.Abstractions;

namespace GitHubActivityReporter.Core.State;

/// <summary>Stores <see cref="ReporterState"/> in <c>.activity-reporter/state.json</c>.</summary>
public sealed class FileReporterStateStore : IReporterStateStore
{
    public const string DefaultDirectoryName = ".activity-reporter";
    public const string DefaultFileName = "state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public FileReporterStateStore(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        StatePath = Path.Combine(Path.GetFullPath(workingDirectory), DefaultDirectoryName, DefaultFileName);
    }

    public string StatePath { get; }

    public async Task<ReporterState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StatePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(StatePath);
        try
        {
            return await JsonSerializer.DeserializeAsync<ReporterState>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // A corrupted state file must not break a run: it is rebuilt on the next success.
            return null;
        }
    }

    public async Task SaveAsync(ReporterState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(StatePath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(state, SerializerOptions);
        await File.WriteAllTextAsync(StatePath, json, cancellationToken).ConfigureAwait(false);
    }
}
