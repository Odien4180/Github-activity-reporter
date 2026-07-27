using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Rendering.Json;

/// <summary>
/// Serializes the report into a normalised JSON document.
/// The document is built from an explicit DTO so no internal or private model can
/// ever be serialized by accident.
/// </summary>
public sealed class JsonReportRenderer : IReportRenderer
{
    public const string DefaultTarget = "generated/report.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string RendererId => KnownRenderers.NormalizedJson;

    public Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var artifact = new RenderedArtifact
        {
            Name = "report.json",
            RelativePath = string.IsNullOrWhiteSpace(context.TargetPath) ? DefaultTarget : context.TargetPath!,
            Content = Render(report, context),
            Kind = RenderedArtifactKind.Json
        };

        return Task.FromResult(new RenderedReport
        {
            RendererId = RendererId,
            Artifacts = [artifact]
        });
    }

    public string Render(ActivityReport report, RendererContext context)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        var privacy = context.Configuration.Privacy;
        var document = new ReportDocument
        {
            SchemaVersion = 1,
            GeneratedAt = report.GeneratedAt,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            User = report.GitHubUserName,
            Public = new PublicSection
            {
                Totals = MapMetrics(report.PublicTotals),
                Repositories = report.PublicActivities
                    .Select((activity, index) => MapRepository(activity, index, privacy.Public))
                    .ToArray()
            },
            Private = MapPrivate(report.PrivateMetrics, privacy.Private)
        };

        return JsonSerializer.Serialize(document, Options);
    }

    private static RepositoryDocument MapRepository(PublicRepositoryActivity activity, int index, PublicPrivacySettings privacy)
        => new()
        {
            Name = privacy.ExposeRepositoryNames ? activity.RepositoryName : $"public-repository-{index + 1}",
            Url = privacy.ExposeRepositoryLinks && privacy.ExposeRepositoryNames ? activity.RepositoryUrl : null,
            Description = privacy.ExposeRepositoryDescriptions ? activity.Description : null,
            Language = privacy.ExposeLanguages ? activity.Language : null,
            Topics = privacy.ExposeTopics ? activity.Topics : Array.Empty<string>(),
            Summary = activity.Summary,
            Metrics = MapMetrics(activity.Metrics),
            Events = activity.Events
                .Where(e => IsTitleAllowed(e.Type, privacy))
                .Select(e => new EventDocument
                {
                    Type = e.Type,
                    Title = e.Title,
                    Url = privacy.ExposeRepositoryLinks ? e.Url : null,
                    OccurredAt = e.OccurredAt
                })
                .ToArray()
        };

    private static bool IsTitleAllowed(ActivityType type, PublicPrivacySettings privacy) => type switch
    {
        ActivityType.PullRequestOpened or ActivityType.PullRequestMerged or ActivityType.PullRequestClosed
            => privacy.ExposePullRequestTitles,
        ActivityType.IssueOpened or ActivityType.IssueClosed => privacy.ExposeIssueTitles,
        ActivityType.ReleasePublished => privacy.ExposeReleaseNames,
        ActivityType.Commit => privacy.ExposeCommitMessages,
        _ => false
    };

    private static MetricsDocument MapMetrics(PublicActivityMetrics metrics)
        => new()
        {
            Commits = metrics.CommitCount,
            PullRequestsOpened = metrics.PullRequestOpenedCount,
            PullRequestsMerged = metrics.PullRequestMergedCount,
            PullRequestsClosed = metrics.PullRequestClosedCount,
            IssuesOpened = metrics.IssueOpenedCount,
            IssuesClosed = metrics.IssueClosedCount,
            Reviews = metrics.ReviewSubmittedCount,
            Releases = metrics.ReleasePublishedCount
        };

    private static PrivateSection MapPrivate(PrivateActivityMetrics metrics, PrivatePrivacySettings privacy)
        => new()
        {
            Mode = "aggregate-only",
            ActiveRepositories = privacy.ExposeActiveRepositoryCount ? metrics.ActiveRepositoryCount : null,
            Commits = privacy.ExposeCommitCount ? metrics.CommitCount : null,
            PullRequestsOpened = privacy.ExposePullRequestOpenedCount ? metrics.PullRequestOpenedCount : null,
            PullRequestsMerged = privacy.ExposePullRequestMergedCount ? metrics.PullRequestMergedCount : null,
            PullRequestsClosed = privacy.ExposePullRequestClosedCount ? metrics.PullRequestClosedCount : null,
            IssuesOpened = privacy.ExposeIssueOpenedCount ? metrics.IssueOpenedCount : null,
            IssuesClosed = privacy.ExposeIssueClosedCount ? metrics.IssueClosedCount : null,
            Reviews = privacy.ExposeReviewCount ? metrics.ReviewSubmittedCount : null,
            Releases = privacy.ExposeReleaseCount ? metrics.ReleasePublishedCount : null,
            ActiveDays = privacy.ExposeActiveDayCount ? metrics.ActiveDayCount : null,
            LastActivityAt = metrics.LastActivityAt
        };

    private sealed record ReportDocument
    {
        [JsonPropertyName("schemaVersion")] public required int SchemaVersion { get; init; }
        [JsonPropertyName("generatedAt")] public required DateTimeOffset GeneratedAt { get; init; }
        [JsonPropertyName("periodStart")] public required DateTimeOffset PeriodStart { get; init; }
        [JsonPropertyName("periodEnd")] public required DateTimeOffset PeriodEnd { get; init; }
        [JsonPropertyName("user")] public required string User { get; init; }
        [JsonPropertyName("public")] public required PublicSection Public { get; init; }
        [JsonPropertyName("private")] public required PrivateSection Private { get; init; }
    }

    private sealed record PublicSection
    {
        [JsonPropertyName("totals")] public required MetricsDocument Totals { get; init; }
        [JsonPropertyName("repositories")] public required IReadOnlyList<RepositoryDocument> Repositories { get; init; }
    }

    private sealed record RepositoryDocument
    {
        [JsonPropertyName("name")] public required string Name { get; init; }
        [JsonPropertyName("url")] public string? Url { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("language")] public string? Language { get; init; }
        [JsonPropertyName("topics")] public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();
        [JsonPropertyName("summary")] public string? Summary { get; init; }
        [JsonPropertyName("metrics")] public required MetricsDocument Metrics { get; init; }
        [JsonPropertyName("events")] public required IReadOnlyList<EventDocument> Events { get; init; }
    }

    private sealed record EventDocument
    {
        [JsonPropertyName("type")] public required ActivityType Type { get; init; }
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("url")] public string? Url { get; init; }
        [JsonPropertyName("occurredAt")] public required DateTimeOffset OccurredAt { get; init; }
    }

    private sealed record MetricsDocument
    {
        [JsonPropertyName("commits")] public required int Commits { get; init; }
        [JsonPropertyName("pullRequestsOpened")] public required int PullRequestsOpened { get; init; }
        [JsonPropertyName("pullRequestsMerged")] public required int PullRequestsMerged { get; init; }
        [JsonPropertyName("pullRequestsClosed")] public required int PullRequestsClosed { get; init; }
        [JsonPropertyName("issuesOpened")] public required int IssuesOpened { get; init; }
        [JsonPropertyName("issuesClosed")] public required int IssuesClosed { get; init; }
        [JsonPropertyName("reviews")] public required int Reviews { get; init; }
        [JsonPropertyName("releases")] public required int Releases { get; init; }
    }

    private sealed record PrivateSection
    {
        [JsonPropertyName("mode")] public required string Mode { get; init; }
        [JsonPropertyName("activeRepositories")] public int? ActiveRepositories { get; init; }
        [JsonPropertyName("commits")] public int? Commits { get; init; }
        [JsonPropertyName("pullRequestsOpened")] public int? PullRequestsOpened { get; init; }
        [JsonPropertyName("pullRequestsMerged")] public int? PullRequestsMerged { get; init; }
        [JsonPropertyName("pullRequestsClosed")] public int? PullRequestsClosed { get; init; }
        [JsonPropertyName("issuesOpened")] public int? IssuesOpened { get; init; }
        [JsonPropertyName("issuesClosed")] public int? IssuesClosed { get; init; }
        [JsonPropertyName("reviews")] public int? Reviews { get; init; }
        [JsonPropertyName("releases")] public int? Releases { get; init; }
        [JsonPropertyName("activeDays")] public int? ActiveDays { get; init; }
        [JsonPropertyName("lastActivityAt")] public DateTimeOffset? LastActivityAt { get; init; }
    }
}
