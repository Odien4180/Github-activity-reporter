using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Security.Tests;

/// <summary>
/// Sample data used by every privacy test. The private strings below are the ones
/// that must never show up in an output, a log or the state file.
/// </summary>
public static class SampleActivity
{
    public const string PublicRepository = "example/public-tool";
    public const string PublicPullRequestTitle = "Improve configuration flow";
    public const string PublicIssueTitle = "Fix connection timeout";

    public const string PrivateRepository = "company/secret-project";
    public const string PrivateOrganization = "company-internal";
    public const string PrivatePullRequestTitle = "Internal Feature Alpha";
    public const string PrivateIssueTitle = "Client-specific defect";
    public const string PrivateBranch = "release/customer-name";
    public const string PrivateFilePath = "Assets/Internal/SecretFeature.cs";
    public const string PrivateCommitMessage = "Implement confidential workflow";

    public static IReadOnlyList<string> PrivateStrings { get; } =
    [
        PrivateRepository,
        "secret-project",
        PrivateOrganization,
        PrivatePullRequestTitle,
        PrivateIssueTitle,
        PrivateBranch,
        PrivateFilePath,
        PrivateCommitMessage
    ];

    public static DateTimeOffset PeriodStart { get; } = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset PeriodEnd { get; } = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    public static CollectionRequest Request { get; } = new()
    {
        UserName = "example-user",
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd
    };

    public static IReadOnlyList<ActivityInput> Inputs { get; } =
    [
        new ActivityInput
        {
            Type = ActivityType.PullRequestMerged,
            RepositoryFullName = PublicRepository,
            IsPrivateRepository = false,
            OccurredAt = PeriodStart.AddDays(1),
            Title = PublicPullRequestTitle,
            Url = $"https://github.com/{PublicRepository}/pull/12",
            RepositoryDescription = "A public tool",
            Language = "C#"
        },
        new ActivityInput
        {
            Type = ActivityType.IssueClosed,
            RepositoryFullName = PublicRepository,
            IsPrivateRepository = false,
            OccurredAt = PeriodStart.AddDays(2),
            Title = PublicIssueTitle,
            Url = $"https://github.com/{PublicRepository}/issues/34"
        },
        new ActivityInput
        {
            Type = ActivityType.Commit,
            RepositoryFullName = PublicRepository,
            IsPrivateRepository = false,
            OccurredAt = PeriodStart.AddDays(2)
        },
        new ActivityInput
        {
            Type = ActivityType.PullRequestOpened,
            RepositoryFullName = PrivateRepository,
            IsPrivateRepository = true,
            OccurredAt = PeriodStart.AddDays(3),
            Title = PrivatePullRequestTitle,
            Url = $"https://github.com/{PrivateRepository}/pull/7",
            RepositoryDescription = "Internal only",
            AdditionalIdentifiers =
            [
                PrivateOrganization,
                PrivateBranch,
                PrivateFilePath,
                PrivateCommitMessage
            ]
        },
        new ActivityInput
        {
            Type = ActivityType.IssueClosed,
            RepositoryFullName = PrivateRepository,
            IsPrivateRepository = true,
            OccurredAt = PeriodStart.AddDays(4),
            Title = PrivateIssueTitle,
            AdditionalIdentifiers = [PrivateOrganization]
        },
        new ActivityInput
        {
            Type = ActivityType.Commit,
            RepositoryFullName = "company/another-secret",
            IsPrivateRepository = true,
            OccurredAt = PeriodStart.AddDays(4),
            AdditionalIdentifiers = [PrivateCommitMessage]
        }
    ];

    public static (CollectedActivity Activity, InMemoryPrivateTermRegistry Registry) Collect()
    {
        var registry = new InMemoryPrivateTermRegistry();
        var builder = new CollectedActivityBuilder(registry);
        builder.AddRange(Inputs, Request);
        return (builder.Build(), registry);
    }

    public static ReporterConfiguration Configuration()
    {
        var configuration = ReporterConfiguration.CreateDefault("example-user");
        configuration.Summary.Language = "en";
        return configuration;
    }
}
