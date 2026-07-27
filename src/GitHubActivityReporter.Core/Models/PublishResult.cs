namespace GitHubActivityReporter.Core.Models;

public enum PublishOutcome
{
    Published,
    NoChanges,
    Skipped,
    Failed
}

/// <summary>Outcome of a single publisher invocation.</summary>
public sealed record PublishResult
{
    public required string PublisherId { get; init; }

    public required PublishOutcome Outcome { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<string> AffectedPaths { get; init; } = Array.Empty<string>();

    public bool IsSuccess => Outcome != PublishOutcome.Failed;

    public static PublishResult Published(string publisherId, IReadOnlyList<string> paths, string? message = null)
        => new() { PublisherId = publisherId, Outcome = PublishOutcome.Published, AffectedPaths = paths, Message = message };

    public static PublishResult NoChanges(string publisherId, string? message = null)
        => new() { PublisherId = publisherId, Outcome = PublishOutcome.NoChanges, Message = message ?? "No changes to publish." };

    public static PublishResult Skipped(string publisherId, string message)
        => new() { PublisherId = publisherId, Outcome = PublishOutcome.Skipped, Message = message };

    public static PublishResult Failed(string publisherId, string message)
        => new() { PublisherId = publisherId, Outcome = PublishOutcome.Failed, Message = message };
}
