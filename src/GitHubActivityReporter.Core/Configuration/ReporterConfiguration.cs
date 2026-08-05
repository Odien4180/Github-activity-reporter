using GitHubActivityReporter.Core.Models;
using YamlDotNet.Serialization;

namespace GitHubActivityReporter.Core.Configuration;

public enum PeriodMode
{
    SinceLastSuccess,
    Last24Hours,
    Last7Days,
    Custom
}

public enum ScheduleFrequency
{
    Daily,
    Weekdays,
    Weekly,
    Manual
}

public enum PrivateExposureMode
{
    AggregateOnly
}

/// <summary>Root of <c>activity-reporter.yml</c>.</summary>
public sealed class ReporterConfiguration
{
    public const string DefaultFileName = "activity-reporter.yml";
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    [YamlMember(Alias = "github")]
    public GitHubSettings GitHub { get; set; } = new();

    public CollectionSettings Collection { get; set; } = new();

    public PrivacySettings Privacy { get; set; } = new();

    public SummarySettings Summary { get; set; } = new();

    public OutputSettings Outputs { get; set; } = new();

    public PublisherSettings Publishers { get; set; } = new();

    public ScheduleSettings Schedule { get; set; } = new();

    public static ReporterConfiguration CreateDefault(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var configuration = new ReporterConfiguration();
        configuration.GitHub.Username = userName;
        configuration.GitHub.ProfileRepository.Owner = userName;
        configuration.GitHub.ProfileRepository.Name = userName;
        return configuration;
    }
}

public sealed class GitHubSettings
{
    public string Username { get; set; } = string.Empty;

    public ProfileRepositorySettings ProfileRepository { get; set; } = new();

    /// <summary>Name of the GitHub Actions secret holding the token. Never the value.</summary>
    public string TokenSecretName { get; set; } = "ACTIVITY_REPORTER_GITHUB_TOKEN";
}

public sealed class ProfileRepositorySettings
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    [YamlIgnore]
    public string FullName => $"{Owner}/{Name}";
}

public sealed class CollectionSettings
{
    public PeriodSettings Period { get; set; } = new();

    public ActivitySourceSettings Public { get; set; } = new();

    public ActivitySourceSettings Private { get; set; } = new()
    {
        EventTypes = new EventTypeSettings
        {
            Commits = true,
            PullRequestsOpened = true,
            PullRequestsMerged = true,
            PullRequestsClosed = false,
            IssuesOpened = false,
            IssuesClosed = true,
            Reviews = true,
            Releases = false
        }
    };
}

public sealed class PeriodSettings
{
    public PeriodMode Mode { get; set; } = PeriodMode.SinceLastSuccess;

    /// <summary>Duration used when no successful run is recorded yet, e.g. <c>24h</c>.</summary>
    public string InitialLookback { get; set; } = "24h";

    /// <summary>Explicit window used when <see cref="Mode"/> is <see cref="PeriodMode.Custom"/>.</summary>
    public string? CustomLookback { get; set; }
}

public sealed class ActivitySourceSettings
{
    public bool Enabled { get; set; } = true;

    public EventTypeSettings EventTypes { get; set; } = new();
}

public sealed class EventTypeSettings
{
    public bool Commits { get; set; } = true;

    public bool PullRequestsOpened { get; set; } = true;

    public bool PullRequestsMerged { get; set; } = true;

    public bool PullRequestsClosed { get; set; }

    public bool IssuesOpened { get; set; } = true;

    public bool IssuesClosed { get; set; } = true;

    public bool Reviews { get; set; } = true;

    public bool Releases { get; set; } = true;

    public IReadOnlySet<ActivityType> ToActivityTypes()
    {
        var types = new HashSet<ActivityType>();
        if (Commits) types.Add(ActivityType.Commit);
        if (PullRequestsOpened) types.Add(ActivityType.PullRequestOpened);
        if (PullRequestsMerged) types.Add(ActivityType.PullRequestMerged);
        if (PullRequestsClosed) types.Add(ActivityType.PullRequestClosed);
        if (IssuesOpened) types.Add(ActivityType.IssueOpened);
        if (IssuesClosed) types.Add(ActivityType.IssueClosed);
        if (Reviews) types.Add(ActivityType.ReviewSubmitted);
        if (Releases) types.Add(ActivityType.ReleasePublished);
        return types;
    }
}

public sealed class PrivacySettings
{
    public PublicPrivacySettings Public { get; set; } = new();

    public PrivatePrivacySettings Private { get; set; } = new();

    /// <summary>Extra strings that must never appear in generated output.</summary>
    public List<string> CustomForbiddenTerms { get; set; } = new();
}

public sealed class PublicPrivacySettings
{
    public bool ExposeRepositoryNames { get; set; } = true;

    public bool ExposeRepositoryLinks { get; set; } = true;

    public bool ExposeRepositoryDescriptions { get; set; } = true;

    public bool ExposePullRequestTitles { get; set; } = true;

    public bool ExposeIssueTitles { get; set; } = true;

    public bool ExposeReleaseNames { get; set; } = true;

    public bool ExposeLanguages { get; set; } = true;

    public bool ExposeTopics { get; set; }

    public bool ExposeCommitMessages { get; set; }

    /// <summary>Optional AI summarisation of public activity, disabled by default.</summary>
    public bool AiSummary { get; set; }
}

public sealed class PrivatePrivacySettings
{
    /// <summary>Only <see cref="PrivateExposureMode.AggregateOnly"/> is supported by design.</summary>
    public PrivateExposureMode Mode { get; set; } = PrivateExposureMode.AggregateOnly;

    public bool ExposeActiveRepositoryCount { get; set; } = true;

    public bool ExposeCommitCount { get; set; } = true;

    public bool ExposePullRequestOpenedCount { get; set; } = true;

    public bool ExposePullRequestMergedCount { get; set; } = true;

    public bool ExposePullRequestClosedCount { get; set; }

    public bool ExposeIssueOpenedCount { get; set; }

    public bool ExposeIssueClosedCount { get; set; } = true;

    public bool ExposeReviewCount { get; set; } = true;

    public bool ExposeReleaseCount { get; set; }

    public bool ExposeActiveDayCount { get; set; } = true;

    // The following switches exist only so a configuration file can be validated:
    // they must always stay false, enabling them is rejected by the configuration validator.
    public bool ExposeRepositoryNames { get; set; }

    public bool ExposeRepositoryAliases { get; set; }

    public bool ExposeOrganizationNames { get; set; }

    public bool ExposeTitles { get; set; }

    public bool ExposeLinks { get; set; }

    public bool ExposeCommitMessages { get; set; }

    public bool ExposeBranchNames { get; set; }

    public bool ExposeFilePaths { get; set; }

    public bool ExposeTopics { get; set; }

    public bool AiSummary { get; set; }
}

public sealed class SummarySettings
{
    public string Language { get; set; } = "ko";

    public string Style { get; set; } = "concise";

    public bool UsePublicChangeDetails { get; set; } = true;

    public string PublicChangeDetailLevel { get; set; } = "standard";

    public int MaxPublicRepositories { get; set; } = 5;

    public int MaxItemsPerRepository { get; set; } = 3;

    public AiSummarySettings Ai { get; set; } = new();
}

public sealed class AiSummarySettings
{
    /// <summary>Supported values are openai and github-models.</summary>
    public string Provider { get; set; } = "openai";

    public string Model { get; set; } = "gpt-5.6-sol";

    public string ApiKeySecretName { get; set; } = "OPENAI_API_KEY";

    /// <summary>
    /// Allows public commit subjects to be used as AI-only evidence. They are never
    /// emitted as raw report events when privacy.public.expose_commit_messages is false.
    /// </summary>
    public bool IncludePublicCommitMessages { get; set; }

    public int MaxInputEvents { get; set; } = 100;

    public int MaxInputCharacters { get; set; } = 20_000;

    public int MaxOutputTokens { get; set; } = 800;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 2;
}

public sealed class OutputSettings
{
    [YamlMember(Alias = "github_profile")]
    public FileOutputSettings GitHubProfile { get; set; } = new()
    {
        Enabled = true,
        Renderer = "compact-markdown",
        Target = "generated/activity.md"
    };

    public FileOutputSettings Json { get; set; } = new()
    {
        Enabled = true,
        Renderer = "normalized-json",
        Target = "generated/report.json"
    };

    public FileOutputSettings Dashboard { get; set; } = new()
    {
        Enabled = false,
        Renderer = "svg-dashboard",
        Target = "generated/activity-dashboard.svg"
    };

    public WebsiteOutputSettings Website { get; set; } = new();

    public EmailOutputSettings Email { get; set; } = new();

    public FileOutputSettings Slack { get; set; } = new()
    {
        Enabled = false,
        Renderer = "slack-blocks",
        Target = "generated/slack.json"
    };
}

public sealed class FileOutputSettings
{
    public bool Enabled { get; set; }

    public string Renderer { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;
}

public sealed class WebsiteOutputSettings
{
    public bool Enabled { get; set; }

    public string Renderer { get; set; } = "static-html";

    public string OutputDirectory { get; set; } = "generated/site";

    public int HistoryDays { get; set; } = 30;
}

public sealed class EmailOutputSettings
{
    public bool Enabled { get; set; }

    public string Renderer { get; set; } = "email-html";

    public string HtmlTarget { get; set; } = "generated/email.html";

    public string TextTarget { get; set; } = "generated/email.txt";
}

public sealed class PublisherSettings
{
    [YamlMember(Alias = "github_profile")]
    public TogglePublisherSettings GitHubProfile { get; set; } = new() { Enabled = true };

    [YamlMember(Alias = "github_pages")]
    public GitHubPagesPublisherSettings GitHubPages { get; set; } = new();

    public SecretPublisherSettings Email { get; set; } = new() { SecretName = "EMAIL_CREDENTIALS" };

    public SecretPublisherSettings Slack { get; set; } = new() { SecretName = "SLACK_WEBHOOK_URL" };

    public LocalPublisherSettings Local { get; set; } = new() { Enabled = true };
}

public sealed class GitHubPagesPublisherSettings
{
    public bool Enabled { get; set; }

    /// <summary>Directory uploaded by the generated GitHub Actions workflow.</summary>
    public string OutputDirectory { get; set; } = "artifacts/pages";
}

public sealed class TogglePublisherSettings
{
    public bool Enabled { get; set; }
}

public sealed class SecretPublisherSettings
{
    public bool Enabled { get; set; }

    /// <summary>Name of the secret. The value is never stored in configuration.</summary>
    public string SecretName { get; set; } = string.Empty;
}

public sealed class LocalPublisherSettings
{
    public bool Enabled { get; set; }

    public string OutputDirectory { get; set; } = "artifacts";
}

public sealed class ScheduleSettings
{
    public bool Enabled { get; set; } = true;

    public string Timezone { get; set; } = "Asia/Seoul";

    public string LocalTime { get; set; } = "09:00";

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

    /// <summary>Resolves the configured IANA time zone, falling back to UTC.</summary>
    public TimeZoneInfo ResolveTimeZone()
    {
        if (string.IsNullOrWhiteSpace(Timezone))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
