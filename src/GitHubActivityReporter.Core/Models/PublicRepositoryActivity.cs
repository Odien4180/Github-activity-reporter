namespace GitHubActivityReporter.Core.Models;

/// <summary>Public activity grouped per repository.</summary>
public sealed record PublicRepositoryActivity
{
    public required string RepositoryName { get; init; }

    public required string RepositoryUrl { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();

    public required IReadOnlyList<PublicActivityEvent> Events { get; init; }

    public required PublicActivityMetrics Metrics { get; init; }

    public string? Summary { get; init; }
}
