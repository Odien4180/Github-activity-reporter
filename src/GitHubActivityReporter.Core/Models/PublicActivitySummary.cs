namespace GitHubActivityReporter.Core.Models;

/// <summary>Period-level public activity narrative shared by every renderer.</summary>
public sealed record PublicActivityNarrative
{
    public string? Headline { get; init; }

    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
}

/// <summary>Complete result of public-only summarisation.</summary>
public sealed record PublicActivitySummary
{
    public required IReadOnlyList<PublicRepositoryActivity> Repositories { get; init; }

    public PublicActivityNarrative Narrative { get; init; } = new();
}
