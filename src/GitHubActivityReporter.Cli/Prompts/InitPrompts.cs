using GitHubActivityReporter.Bootstrap.ConfigurationSetup;
using GitHubActivityReporter.Core.Configuration;
using Spectre.Console;

namespace GitHubActivityReporter.Cli.Prompts;

/// <summary>
/// Interactive questions of the <c>init</c> command. When the console is not
/// interactive every question falls back to the documented default.
/// </summary>
public sealed class InitPrompts
{
    private readonly IAnsiConsole _console;
    private readonly bool _interactive;

    public InitPrompts(IAnsiConsole? console = null, bool interactive = true)
    {
        _console = console ?? AnsiConsole.Console;
        _interactive = interactive && !Console.IsInputRedirected;
    }

    public bool IsInteractive => _interactive;

    public bool Confirm(string question, bool defaultValue = true)
    {
        if (!_interactive)
        {
            return defaultValue;
        }

        return _console.Prompt(new ConfirmationPrompt(question) { DefaultValue = defaultValue });
    }

    public string Ask(string question, string defaultValue)
    {
        if (!_interactive)
        {
            return defaultValue;
        }

        return _console.Prompt(new TextPrompt<string>(question).DefaultValue(defaultValue));
    }

    public T Choose<T>(string title, IReadOnlyList<(string Label, T Value)> choices, T defaultValue)
        where T : notnull
    {
        if (!_interactive || choices.Count == 0)
        {
            return defaultValue;
        }

        var prompt = new SelectionPrompt<string>()
            .Title(title)
            .AddChoices(choices.Select(c => c.Label));

        var selected = _console.Prompt(prompt);
        return choices.First(c => c.Label == selected).Value;
    }

    public IReadOnlyList<string> MultiSelect(
        string title,
        IReadOnlyList<string> options,
        IReadOnlyList<string> defaults)
    {
        if (!_interactive || options.Count == 0)
        {
            return defaults;
        }

        var prompt = new MultiSelectionPrompt<string>()
            .Title(title)
            .NotRequired()
            .InstructionsText("[grey](space to toggle, enter to accept)[/]");

        foreach (var option in options)
        {
            prompt.AddChoice(option);
        }

        foreach (var selected in defaults.Where(options.Contains))
        {
            prompt.Select(selected);
        }

        return _console.Prompt(prompt);
    }

    public InitAnswers Collect(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var periodMode = Choose(
            "Reporting period",
            [
                ("Since the last successful run", PeriodMode.SinceLastSuccess),
                ("Last 24 hours", PeriodMode.Last24Hours),
                ("Last 7 days", PeriodMode.Last7Days),
                ("Custom lookback", PeriodMode.Custom)
            ],
            PeriodMode.SinceLastSuccess);

        var lookback = periodMode == PeriodMode.Custom
            ? Ask("Custom lookback (for example 12h or 3d)", "24h")
            : Ask("Initial lookback used on the very first run", "24h");

        var collectPublic = Confirm("Collect public repository activity?");
        var collectPrivate = Confirm("Collect private repository activity (aggregate counters only)?");

        var publicTypes = MultiSelect(
            "Public activity types",
            ["commits", "pull requests opened", "pull requests merged", "pull requests closed", "issues opened", "issues closed", "reviews", "releases"],
            ["commits", "pull requests opened", "pull requests merged", "issues opened", "issues closed", "reviews", "releases"]);

        var privateTypes = MultiSelect(
            "Private activity counters",
            ["commits", "pull requests opened", "pull requests merged", "pull requests closed", "issues opened", "issues closed", "reviews", "releases"],
            ["commits", "pull requests opened", "pull requests merged", "issues closed", "reviews"]);

        var publicFields = MultiSelect(
            "Public details to expose",
            ["repository names", "repository links", "repository descriptions", "pull request titles", "issue titles", "release names", "languages", "topics", "commit messages"],
            ["repository names", "repository links", "repository descriptions", "pull request titles", "issue titles", "release names", "languages"]);

        var privateCounters = MultiSelect(
            "Private counters to publish (identifiers are never available)",
            ["active repository count", "commits", "pull requests opened", "pull requests merged", "pull requests closed", "issues opened", "issues closed", "reviews", "releases", "active days"],
            ["active repository count", "commits", "pull requests opened", "pull requests merged", "issues closed", "reviews", "active days"]);

        var outputs = MultiSelect(
            "Outputs to generate",
            ["GitHub profile markdown", "JSON report"],
            ["GitHub profile markdown", "JSON report"]);

        var publishers = MultiSelect(
            "Where should the outputs be published?",
            ["GitHub profile repository", "local directory"],
            ["GitHub profile repository", "local directory"]);

        var frequency = Choose(
            "Update frequency",
            [
                ("Daily", ScheduleFrequency.Daily),
                ("Weekdays only", ScheduleFrequency.Weekdays),
                ("Weekly", ScheduleFrequency.Weekly),
                ("Manual runs only", ScheduleFrequency.Manual)
            ],
            ScheduleFrequency.Daily);

        var localTime = frequency == ScheduleFrequency.Manual ? "09:00" : Ask("Local run time (HH:mm)", "09:00");
        var timezone = Ask("Time zone (IANA id)", "Asia/Seoul");
        var language = Choose("Report language", [("한국어 (ko)", "ko"), ("English (en)", "en")], "ko");

        return new InitAnswers
        {
            UserName = userName,
            ProfileRepositoryOwner = userName,
            ProfileRepositoryName = userName,
            PeriodMode = periodMode,
            InitialLookback = lookback,
            CollectPublic = collectPublic,
            CollectPrivate = collectPrivate,
            PublicEventTypes = BuildEventTypes(publicTypes),
            PrivateEventTypes = BuildEventTypes(privateTypes),
            PublicPrivacy = BuildPublicPrivacy(publicFields),
            PrivatePrivacy = BuildPrivatePrivacy(privateCounters),
            MarkdownOutput = outputs.Contains("GitHub profile markdown"),
            JsonOutput = outputs.Contains("JSON report"),
            PublishToProfileRepository = publishers.Contains("GitHub profile repository"),
            PublishToLocalDirectory = publishers.Contains("local directory"),
            Frequency = frequency,
            LocalTime = localTime,
            Timezone = timezone,
            Language = language
        };
    }

    private static EventTypeSettings BuildEventTypes(IReadOnlyList<string> selected) => new()
    {
        Commits = selected.Contains("commits"),
        PullRequestsOpened = selected.Contains("pull requests opened"),
        PullRequestsMerged = selected.Contains("pull requests merged"),
        PullRequestsClosed = selected.Contains("pull requests closed"),
        IssuesOpened = selected.Contains("issues opened"),
        IssuesClosed = selected.Contains("issues closed"),
        Reviews = selected.Contains("reviews"),
        Releases = selected.Contains("releases")
    };

    private static PublicPrivacySettings BuildPublicPrivacy(IReadOnlyList<string> selected) => new()
    {
        ExposeRepositoryNames = selected.Contains("repository names"),
        ExposeRepositoryLinks = selected.Contains("repository links"),
        ExposeRepositoryDescriptions = selected.Contains("repository descriptions"),
        ExposePullRequestTitles = selected.Contains("pull request titles"),
        ExposeIssueTitles = selected.Contains("issue titles"),
        ExposeReleaseNames = selected.Contains("release names"),
        ExposeLanguages = selected.Contains("languages"),
        ExposeTopics = selected.Contains("topics"),
        ExposeCommitMessages = selected.Contains("commit messages"),
        AiSummary = false
    };

    private static PrivatePrivacySettings BuildPrivatePrivacy(IReadOnlyList<string> selected) => new()
    {
        Mode = PrivateExposureMode.AggregateOnly,
        ExposeActiveRepositoryCount = selected.Contains("active repository count"),
        ExposeCommitCount = selected.Contains("commits"),
        ExposePullRequestOpenedCount = selected.Contains("pull requests opened"),
        ExposePullRequestMergedCount = selected.Contains("pull requests merged"),
        ExposePullRequestClosedCount = selected.Contains("pull requests closed"),
        ExposeIssueOpenedCount = selected.Contains("issues opened"),
        ExposeIssueClosedCount = selected.Contains("issues closed"),
        ExposeReviewCount = selected.Contains("reviews"),
        ExposeReleaseCount = selected.Contains("releases"),
        ExposeActiveDayCount = selected.Contains("active days")
    };
}
