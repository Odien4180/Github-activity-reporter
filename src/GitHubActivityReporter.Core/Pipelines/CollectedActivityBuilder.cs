using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Core.Pipelines;

/// <summary>
/// Single funnel every collector uses. It classifies raw activity by visibility,
/// filters it by the requested period and event types, converts private activity
/// into opaque events and registers private identifiers so the privacy validator
/// can later prove they never reached an output.
/// </summary>
public sealed class CollectedActivityBuilder
{
    private readonly IPrivateTermRegistry _privateTerms;
    private readonly List<PublicActivityEvent> _publicEvents = new();
    private readonly List<PrivateActivityEvent> _privateEvents = new();

    public CollectedActivityBuilder(IPrivateTermRegistry privateTerms)
    {
        _privateTerms = privateTerms ?? throw new ArgumentNullException(nameof(privateTerms));
    }

    public int PublicEventCount => _publicEvents.Count;

    public int PrivateEventCount => _privateEvents.Count;

    /// <summary>Adds one raw activity. Returns the visibility it was classified as, or null when filtered out.</summary>
    public ActivityVisibility? Add(ActivityInput input, CollectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Contains(input.OccurredAt))
        {
            return null;
        }

        if (request.IsRepositoryExcluded(input.RepositoryFullName))
        {
            return null;
        }

        if (input.IsPrivateRepository)
        {
            return AddPrivate(input, request);
        }

        return AddPublic(input, request);
    }

    public int AddRange(IEnumerable<ActivityInput> inputs, CollectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var added = 0;
        foreach (var input in inputs)
        {
            if (Add(input, request) is not null)
            {
                added++;
            }
        }

        return added;
    }

    public CollectedActivity Build()
    {
        var publicEvents = EventDeduplicator.DeduplicatePublic(_publicEvents)
            .OrderByDescending(e => e.OccurredAt)
            .ToArray();

        var privateEvents = EventDeduplicator.DeduplicatePrivate(_privateEvents)
            .OrderByDescending(e => e.OccurredAt)
            .ToArray();

        return new CollectedActivity(publicEvents, privateEvents);
    }

    private ActivityVisibility? AddPublic(ActivityInput input, CollectionRequest request)
    {
        if (!request.CollectPublic || !request.PublicEventTypes.Contains(input.Type))
        {
            return null;
        }

        _publicEvents.Add(new PublicActivityEvent
        {
            Type = input.Type,
            RepositoryName = input.RepositoryFullName,
            RepositoryUrl = input.RepositoryUrl ?? BuildRepositoryUrl(input.RepositoryFullName),
            Title = input.Title,
            Url = input.Url,
            Description = input.RepositoryDescription,
            Language = input.Language,
            Topics = input.Topics,
            OccurredAt = input.OccurredAt
        });

        return ActivityVisibility.Public;
    }

    private ActivityVisibility? AddPrivate(ActivityInput input, CollectionRequest request)
    {
        // Private identifiers are registered unconditionally so that the privacy
        // validator can prove they never reached an output.
        RegisterPrivateTerms(input);

        if (!request.CollectPrivate)
        {
            return null;
        }

        // Activity counts are always collected regardless of the event-type filter
        // so that the metrics reflect the real volume of private work.
        _privateEvents.Add(new PrivateActivityEvent
        {
            RepositoryOpaqueId = OpaqueIdentifier.Create(input.RepositoryFullName),
            Type = input.Type,
            OccurredAt = input.OccurredAt
        });

        return ActivityVisibility.Private;
    }

    private void RegisterPrivateTerms(ActivityInput input)
    {
        _privateTerms.Add(input.RepositoryFullName);
        _privateTerms.Add(input.RepositoryUrl);
        _privateTerms.Add(input.Title);
        _privateTerms.Add(input.Url);
        _privateTerms.Add(input.RepositoryDescription);
        _privateTerms.AddRange(input.AdditionalIdentifiers);

        var separator = input.RepositoryFullName.IndexOf('/');
        if (separator > 0)
        {
            _privateTerms.Add(input.RepositoryFullName[..separator]);
            _privateTerms.Add(input.RepositoryFullName[(separator + 1)..]);
        }
    }

    private static string BuildRepositoryUrl(string fullName) => $"https://github.com/{fullName}";
}
