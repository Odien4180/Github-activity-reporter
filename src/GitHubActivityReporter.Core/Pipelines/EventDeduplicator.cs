using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Pipelines;

/// <summary>
/// Removes duplicated events. The same activity can be observed through several
/// GitHub API paths, so collectors always run their results through this class.
/// </summary>
public static class EventDeduplicator
{
    /// <summary>
    /// Public events are identified by repository, type, url and timestamp.
    /// Commit events are passed through: a single push produces several commits that
    /// share one timestamp, and collectors already de-duplicate at raw event level.
    /// </summary>
    public static IReadOnlyList<PublicActivityEvent> DeduplicatePublic(IEnumerable<PublicActivityEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var seen = new HashSet<(string Repository, ActivityType Type, string Key, DateTimeOffset OccurredAt)>();
        var result = new List<PublicActivityEvent>();

        foreach (var candidate in events)
        {
            if (candidate.Type == ActivityType.Commit)
            {
                result.Add(candidate);
                continue;
            }

            var key = candidate.Url ?? candidate.Title ?? string.Empty;
            if (seen.Add((candidate.RepositoryName, candidate.Type, key, candidate.OccurredAt)))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// Private events are identified by their opaque repository id, type and timestamp.
    /// Commit events are excluded because a single push legitimately produces several
    /// commits sharing the very same timestamp; those are de-duplicated by the collector
    /// at raw event level instead.
    /// </summary>
    internal static IReadOnlyList<PrivateActivityEvent> DeduplicatePrivate(IEnumerable<PrivateActivityEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var seen = new HashSet<(string Repository, ActivityType Type, DateTimeOffset OccurredAt)>();
        var result = new List<PrivateActivityEvent>();

        foreach (var candidate in events)
        {
            if (candidate.Type == ActivityType.Commit)
            {
                result.Add(candidate);
                continue;
            }

            if (seen.Add((candidate.RepositoryOpaqueId, candidate.Type, candidate.OccurredAt)))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
