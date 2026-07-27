using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Bootstrap.ConfigurationSetup;

/// <summary>Answers gathered by the interactive <c>init</c> command.</summary>
public sealed record InitAnswers
{
    public required string UserName { get; init; }

    public string? ProfileRepositoryOwner { get; init; }

    public string? ProfileRepositoryName { get; init; }

    public string Branch { get; init; } = "main";

    public bool CollectPublic { get; init; } = true;

    public bool CollectPrivate { get; init; } = true;

    public PeriodMode PeriodMode { get; init; } = PeriodMode.SinceLastSuccess;

    public string InitialLookback { get; init; } = "24h";

    public EventTypeSettings PublicEventTypes { get; init; } = new();

    public EventTypeSettings PrivateEventTypes { get; init; } = new()
    {
        Commits = true,
        PullRequestsOpened = true,
        PullRequestsMerged = true,
        PullRequestsClosed = false,
        IssuesOpened = false,
        IssuesClosed = true,
        Reviews = true,
        Releases = false
    };

    public PublicPrivacySettings PublicPrivacy { get; init; } = new();

    public PrivatePrivacySettings PrivatePrivacy { get; init; } = new();

    public bool MarkdownOutput { get; init; } = true;

    public bool JsonOutput { get; init; } = true;

    public bool PublishToProfileRepository { get; init; } = true;

    public bool PublishToLocalDirectory { get; init; } = true;

    public string LocalOutputDirectory { get; init; } = "artifacts";

    public string Timezone { get; init; } = "Asia/Seoul";

    public string LocalTime { get; init; } = "09:00";

    public ScheduleFrequency Frequency { get; init; } = ScheduleFrequency.Daily;

    public string Language { get; init; } = "ko";

    public int MaxPublicRepositories { get; init; } = 5;

    public int MaxItemsPerRepository { get; init; } = 3;
}
