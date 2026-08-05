using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.GitHub.Api;

/// <summary>
/// Normalised GitHub event. Internal on purpose: instances that come from a private
/// repository still carry identifying strings and must never leave this assembly.
/// </summary>
internal sealed record GitHubRawEvent
{
    /// <summary>GitHub event id, used to remove duplicates at source level.</summary>
    public required string Id { get; init; }

    public required ActivityType Type { get; init; }

    public required string RepositoryFullName { get; init; }

    public required bool IsPrivateRepository { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

    public int? Additions { get; init; }

    public int? Deletions { get; init; }

    public int? ChangedFiles { get; init; }
}

/// <summary>Public repository metadata used to enrich public activity.</summary>
internal sealed record GitHubRepositoryInfo
{
    public required string FullName { get; init; }

    public required string HtmlUrl { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();
}

internal interface IGitHubEventSource
{
    Task<IReadOnlyList<GitHubRawEvent>> GetUserEventsAsync(
        string userName,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    string? LastDiagnostics { get; }

    /// <summary>Only ever called for public repositories.</summary>
    Task<GitHubRepositoryInfo?> GetPublicRepositoryAsync(
        string fullName,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a git compare call for a push event.
/// Carries commit count and diff statistics so the mapper can enrich events without
/// making redundant API calls.
/// </summary>
internal sealed record PushCompareResult
{
    public int? CommitCount { get; init; }

    public IReadOnlyList<string> CommitSubjects { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

    public int? Additions { get; init; }

    public int? Deletions { get; init; }

    public int? ChangedFiles { get; init; }
}
