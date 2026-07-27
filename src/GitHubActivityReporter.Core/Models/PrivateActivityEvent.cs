namespace GitHubActivityReporter.Core.Models;

/// <summary>
/// A single activity that happened in a private repository.
/// This type is intentionally <c>internal</c>: private raw activity must never
/// reach a public API surface, a renderer, a summarizer or a serialized file.
/// Only the aggregated <see cref="PrivateActivityMetrics"/> may leave the pipeline.
/// </summary>
internal sealed record PrivateActivityEvent
{
    /// <summary>
    /// Non reversible identifier used only to count distinct active repositories
    /// and to remove duplicated events. It is discarded after aggregation.
    /// </summary>
    public required string RepositoryOpaqueId { get; init; }

    public required ActivityType Type { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
