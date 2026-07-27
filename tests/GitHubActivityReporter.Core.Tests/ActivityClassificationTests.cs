using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.Validation;

namespace GitHubActivityReporter.Core.Tests;

public sealed class ActivityClassificationTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    private static CollectionRequest Request(
        bool collectPublic = true,
        bool collectPrivate = true,
        IReadOnlySet<ActivityType>? publicTypes = null,
        IReadOnlySet<ActivityType>? privateTypes = null) => new()
    {
        UserName = "example-user",
        PeriodStart = Start,
        PeriodEnd = End,
        CollectPublic = collectPublic,
        CollectPrivate = collectPrivate,
        PublicEventTypes = publicTypes ?? CollectionRequest.AllTypes,
        PrivateEventTypes = privateTypes ?? CollectionRequest.AllTypes
    };

    private static ActivityInput Input(
        bool isPrivate,
        ActivityType type = ActivityType.Commit,
        DateTimeOffset? occurredAt = null,
        string repository = "example/tool") => new()
    {
        Type = type,
        RepositoryFullName = repository,
        IsPrivateRepository = isPrivate,
        OccurredAt = occurredAt ?? Start.AddDays(1)
    };

    [Fact]
    public void Public_events_are_classified_as_public()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());

        var visibility = builder.Add(Input(isPrivate: false), Request());

        Assert.Equal(ActivityVisibility.Public, visibility);
        Assert.Equal(1, builder.PublicEventCount);
        Assert.Equal(0, builder.PrivateEventCount);
    }

    [Fact]
    public void Private_events_are_classified_as_private_and_lose_every_identifier()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());

        var visibility = builder.Add(
            Input(isPrivate: true, repository: "company/secret") with
            {
                Title = "Internal work",
                Url = "https://github.com/company/secret/pull/1"
            },
            Request());

        var collected = builder.Build();

        Assert.Equal(ActivityVisibility.Private, visibility);
        Assert.Empty(collected.PublicEvents);
        Assert.Equal(1, collected.PrivateEventCount);

        var privateEvent = Assert.Single(collected.PrivateEvents);
        Assert.NotEqual("company/secret", privateEvent.RepositoryOpaqueId);
        Assert.DoesNotContain("secret", privateEvent.RepositoryOpaqueId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_same_private_repository_always_maps_to_the_same_opaque_id()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        builder.Add(Input(isPrivate: true, repository: "company/secret", occurredAt: Start.AddDays(1)), Request());
        builder.Add(Input(isPrivate: true, repository: "company/secret", occurredAt: Start.AddDays(2)), Request());
        builder.Add(Input(isPrivate: true, repository: "company/other", occurredAt: Start.AddDays(2)), Request());

        var collected = builder.Build();

        Assert.Equal(2, collected.PrivateEvents.Select(e => e.RepositoryOpaqueId).Distinct().Count());
    }

    [Fact]
    public void Events_outside_the_period_are_dropped()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());

        var before = builder.Add(Input(isPrivate: false, occurredAt: Start.AddDays(-1)), Request());
        var after = builder.Add(Input(isPrivate: false, occurredAt: End.AddMinutes(1)), Request());
        var inside = builder.Add(Input(isPrivate: false, occurredAt: Start.AddHours(1)), Request());

        Assert.Null(before);
        Assert.Null(after);
        Assert.Equal(ActivityVisibility.Public, inside);
        Assert.Equal(1, builder.PublicEventCount);
    }

    [Fact]
    public void Disabled_event_types_are_filtered_per_visibility()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        var request = Request(
            publicTypes: new HashSet<ActivityType> { ActivityType.Commit },
            privateTypes: new HashSet<ActivityType> { ActivityType.PullRequestMerged });

        builder.Add(Input(isPrivate: false, ActivityType.IssueClosed), request);
        builder.Add(Input(isPrivate: false, ActivityType.Commit), request);
        builder.Add(Input(isPrivate: true, ActivityType.Commit, repository: "company/secret"), request);
        builder.Add(Input(isPrivate: true, ActivityType.PullRequestMerged, repository: "company/secret"), request);

        // Public events are still filtered by event type.
        Assert.Equal(1, builder.PublicEventCount);
        // Private events are always counted regardless of the event-type filter so
        // that metrics reflect the real volume of private work.
        Assert.Equal(2, builder.PrivateEventCount);
    }

    [Fact]
    public void Collection_can_be_disabled_per_visibility()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        var request = Request(collectPublic: false, collectPrivate: true);

        builder.Add(Input(isPrivate: false), request);
        builder.Add(Input(isPrivate: true, repository: "company/secret"), request);

        Assert.Equal(0, builder.PublicEventCount);
        Assert.Equal(1, builder.PrivateEventCount);
    }

    [Fact]
    public void Private_identifiers_are_registered_even_when_the_event_is_filtered_out()
    {
        var registry = new InMemoryPrivateTermRegistry();
        var builder = new CollectedActivityBuilder(registry);
        var request = Request(collectPrivate: false);

        builder.Add(
            Input(isPrivate: true, ActivityType.IssueClosed, repository: "company/secret-project") with
            {
                Title = "Client-specific defect",
                AdditionalIdentifiers = ["company-internal", "release/customer-name"]
            },
            request);

        Assert.Equal(0, builder.PrivateEventCount);
        Assert.Contains("company/secret-project", registry.Terms);
        Assert.Contains("secret-project", registry.Terms);
        Assert.Contains("company", registry.Terms);
        Assert.Contains("Client-specific defect", registry.Terms);
        Assert.Contains("company-internal", registry.Terms);
        Assert.Contains("release/customer-name", registry.Terms);
    }

    [Fact]
    public void Public_events_keep_their_metadata()
    {
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        builder.Add(
            Input(isPrivate: false, ActivityType.PullRequestMerged, repository: "example/public-tool") with
            {
                Title = "Improve configuration flow",
                Url = "https://github.com/example/public-tool/pull/12",
                RepositoryDescription = "A public tool",
                Language = "C#",
                Topics = ["dotnet"]
            },
            Request());

        var collected = builder.Build();
        var item = Assert.Single(collected.PublicEvents);

        Assert.Equal("example/public-tool", item.RepositoryName);
        Assert.Equal("https://github.com/example/public-tool", item.RepositoryUrl);
        Assert.Equal("Improve configuration flow", item.Title);
        Assert.Equal("C#", item.Language);
        Assert.Equal(["dotnet"], item.Topics);
    }

    [Fact]
    public void GitHub_username_is_excluded_from_forbidden_terms_in_validation_context()
    {
        // If the user owns a private repo (username/private-repo), the owner segment
        // would normally be added to the registry. It must NOT appear in ForbiddenTerms
        // because the username legitimately appears in public report outputs.
        const string userName = "my-github-user";
        var registry = new InMemoryPrivateTermRegistry();
        var builder = new CollectedActivityBuilder(registry);

        builder.Add(
            new ActivityInput
            {
                Type = ActivityType.Commit,
                RepositoryFullName = $"{userName}/private-work",
                IsPrivateRepository = true,
                OccurredAt = Start.AddDays(1)
            },
            Request());

        // The registry must contain the username as a segment of the full repo name.
        Assert.Contains(userName, registry.Terms, StringComparer.OrdinalIgnoreCase);

        var configuration = ReporterConfiguration.CreateDefault(userName);
        var context = ValidationContext.Create(registry, configuration);

        // But the validation context must exclude it so public outputs that reference
        // the username are not incorrectly flagged.
        Assert.DoesNotContain(userName, context.ForbiddenTerms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Private_event_type_filter_does_not_suppress_counts()
    {
        // Even when a specific event type (e.g. Commit) is excluded from the private
        // event-type filter, that event must still be counted in the metrics.
        var builder = new CollectedActivityBuilder(new InMemoryPrivateTermRegistry());
        var request = Request(
            privateTypes: new HashSet<ActivityType> { ActivityType.PullRequestMerged });

        builder.Add(Input(isPrivate: true, ActivityType.Commit, repository: "company/secret"), request);
        builder.Add(Input(isPrivate: true, ActivityType.PullRequestMerged, repository: "company/secret"), request);

        // Both events are counted regardless of the filter.
        Assert.Equal(2, builder.PrivateEventCount);
    }
}
