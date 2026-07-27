namespace GitHubActivityReporter.Core.Models;

/// <summary>
/// A single activity that happened in a public repository.
/// Only data that is already publicly visible on GitHub is carried here.
/// </summary>
public sealed record PublicActivityEvent
{
    public required ActivityType Type { get; init; }

    public required string RepositoryName { get; init; }

    public required string RepositoryUrl { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();

    public required DateTimeOffset OccurredAt { get; init; }
}
