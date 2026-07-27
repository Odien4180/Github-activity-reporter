namespace GitHubActivityReporter.Core.Models;

/// <summary>Describes what should be collected for a single run.</summary>
public sealed record CollectionRequest
{
    public required string UserName { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public bool CollectPublic { get; init; } = true;

    public bool CollectPrivate { get; init; } = true;

    /// <summary>Public activity types that should be kept.</summary>
    public IReadOnlySet<ActivityType> PublicEventTypes { get; init; } = AllTypes;

    /// <summary>Private activity types that should be counted.</summary>
    public IReadOnlySet<ActivityType> PrivateEventTypes { get; init; } = AllTypes;

    public static IReadOnlySet<ActivityType> AllTypes { get; } =
        new HashSet<ActivityType>(Enum.GetValues<ActivityType>());

    public bool Contains(DateTimeOffset moment)
        => moment >= PeriodStart && moment <= PeriodEnd;
}
