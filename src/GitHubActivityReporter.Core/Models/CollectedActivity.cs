namespace GitHubActivityReporter.Core.Models;

/// <summary>
/// Result of an activity collection run.
/// Public events are exposed as-is, private raw events are kept internal and are
/// only reachable by the aggregation pipeline inside this assembly.
/// </summary>
public sealed class CollectedActivity
{
    private readonly IReadOnlyList<PrivateActivityEvent> _privateEvents;

    internal CollectedActivity(
        IReadOnlyList<PublicActivityEvent> publicEvents,
        IReadOnlyList<PrivateActivityEvent> privateEvents)
    {
        PublicEvents = publicEvents ?? throw new ArgumentNullException(nameof(publicEvents));
        _privateEvents = privateEvents ?? throw new ArgumentNullException(nameof(privateEvents));
    }

    /// <summary>Public repository events, safe to render.</summary>
    public IReadOnlyList<PublicActivityEvent> PublicEvents { get; }

    /// <summary>Private raw events. Never leaves this assembly boundary.</summary>
    internal IReadOnlyList<PrivateActivityEvent> PrivateEvents => _privateEvents;

    /// <summary>Number of collected private events. Safe: it is a plain counter.</summary>
    public int PrivateEventCount => _privateEvents.Count;

    /// <summary>Creates a collection result that only contains public activity.</summary>
    public static CollectedActivity FromPublicEvents(IReadOnlyList<PublicActivityEvent> publicEvents)
        => new(publicEvents, Array.Empty<PrivateActivityEvent>());

    public static CollectedActivity Empty { get; } =
        new(Array.Empty<PublicActivityEvent>(), Array.Empty<PrivateActivityEvent>());
}
