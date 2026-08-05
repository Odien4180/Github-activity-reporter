using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Pipelines;

/// <summary>
/// Raw, not yet classified activity as it arrives from a collector.
/// It is a transient value: private instances are converted into opaque events
/// immediately by <see cref="CollectedActivityBuilder"/> and then dropped.
/// </summary>
public sealed record ActivityInput
{
    public required ActivityType Type { get; init; }

    public required string RepositoryFullName { get; init; }

    public required bool IsPrivateRepository { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public string? RepositoryUrl { get; init; }

    public string? RepositoryDescription { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

    public int? Additions { get; init; }

    public int? Deletions { get; init; }

    public int? ChangedFiles { get; init; }

    /// <summary>Additional strings that must be treated as private identifiers (branch, organisation, ...).</summary>
    public IReadOnlyList<string> AdditionalIdentifiers { get; init; } = Array.Empty<string>();

    public ActivityVisibility Visibility => IsPrivateRepository ? ActivityVisibility.Private : ActivityVisibility.Public;
}
