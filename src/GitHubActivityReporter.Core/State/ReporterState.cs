using System.Text.Json.Serialization;

namespace GitHubActivityReporter.Core.State;

/// <summary>
/// Persisted state of the reporter. It contains no activity data at all:
/// only the schema version, the tool version, the last successful run and a hash.
/// </summary>
public sealed record ReporterState
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("reporterVersion")]
    public string ReporterVersion { get; init; } = ReporterVersionInfo.Version;

    [JsonPropertyName("lastSuccessfulRunAt")]
    public DateTimeOffset? LastSuccessfulRunAt { get; init; }

    [JsonPropertyName("lastReportHash")]
    public string? LastReportHash { get; init; }
}

public static class ReporterVersionInfo
{
    public const string Version = "0.1.0";
}
