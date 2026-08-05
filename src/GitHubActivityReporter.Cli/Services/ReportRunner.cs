using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.State;
using GitHubActivityReporter.Core.Validation;
using GitHubActivityReporter.Publishing.FileSystem;
using GitHubActivityReporter.Publishing.GitHubProfile;
using GitHubActivityReporter.Publishing.GitHubPages;
using GitHubActivityReporter.Publishing.Email;
using GitHubActivityReporter.Publishing.Slack;
using GitHubActivityReporter.Rendering.Html;
using GitHubActivityReporter.Rendering.Email;
using GitHubActivityReporter.Rendering.Json;
using GitHubActivityReporter.Rendering.Markdown;
using GitHubActivityReporter.Rendering.Slack;
using GitHubActivityReporter.Rendering.Svg;
using GitHubActivityReporter.Summarization.RuleBased;
using GitHubActivityReporter.Summarization.AI;
using GitHubActivityReporter.Summarization.Fallback;

namespace GitHubActivityReporter.Cli.Services;

public sealed record RunOptions
{
    public required ReporterConfiguration Configuration { get; init; }

    public required string WorkingDirectory { get; init; }

    /// <summary>Preview never invokes a publisher.</summary>
    public bool Preview { get; init; }

    /// <summary>Publishers are invoked but must not perform irreversible side effects.</summary>
    public bool DryRun { get; init; }

    public bool CommitProfileRepository { get; init; }

    public bool PushProfileRepository { get; init; }

    public string? ProfileRepositoryPath { get; init; }
}

public sealed record RunOutcome
{
    public required ActivityReport Report { get; init; }

    public required PipelineResult Pipeline { get; init; }

    public required ReportPeriod Period { get; init; }

    public required string ReportHash { get; init; }

    public bool Succeeded => Pipeline.Succeeded;
}

/// <summary>
/// Wires collection, summarisation, rendering, privacy validation, publishing and
/// state persistence together. Used by <c>run</c> and <c>preview</c>.
/// </summary>
public sealed class ReportRunner
{
    private readonly IActivityCollector _collector;
    private readonly IPrivateTermRegistry _privateTerms;
    private readonly IReporterStateStore _stateStore;
    private readonly IClock _clock;
    private readonly IReporterLog _log;

    public ReportRunner(
        IActivityCollector collector,
        IPrivateTermRegistry privateTerms,
        IReporterStateStore stateStore,
        IClock? clock = null,
        IReporterLog? log = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _privateTerms = privateTerms ?? throw new ArgumentNullException(nameof(privateTerms));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _clock = clock ?? SystemClock.Instance;
        _log = log ?? NullReporterLog.Instance;
    }

    public async Task<RunOutcome> ExecuteAsync(RunOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuration = options.Configuration;
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var period = new PeriodResolver(_clock).Resolve(configuration, state);

        var request = new CollectionRequest
        {
            UserName = configuration.GitHub.Username,
            PeriodStart = period.Start,
            PeriodEnd = period.End,
            CollectPublic = configuration.Collection.Public.Enabled,
            CollectPrivate = configuration.Collection.Private.Enabled,
            PublicEventTypes = configuration.Collection.Public.EventTypes.ToActivityTypes(),
            PrivateEventTypes = configuration.Collection.Private.EventTypes.ToActivityTypes(),
            ExcludedRepositoryFullNames = BuildExcludedRepositories(configuration)
        };

        var collected = await _collector.CollectAsync(request, cancellationToken).ConfigureAwait(false);

        var summarizer = BuildSummarizer(configuration);
        var reportBuilder = new ActivityReportBuilder(summarizer, _clock);
        var report = await reportBuilder
            .BuildAsync(collected, new ReportBuildContext
            {
                GitHubUserName = configuration.GitHub.Username,
                Period = period
            }, cancellationToken)
            .ConfigureAwait(false);

        var pipeline = new ReportPipeline(
            BuildRenderers(configuration),
            BuildPublishers(options, _log),
            new PrivacyValidator(),
            _log);

        var pipelineOptions = new PipelineOptions
        {
            Configuration = configuration,
            WorkingDirectory = options.WorkingDirectory,
            PreviewMode = options.Preview,
            DryRun = options.DryRun,
            ValidationContext = ValidationContext.Create(_privateTerms, configuration),
            PublisherOptions = BuildPublisherOptions(options)
        };

        var pipelineResult = await pipeline.ExecuteAsync(report, pipelineOptions, cancellationToken).ConfigureAwait(false);
        var hash = ReportHasher.Hash(pipelineResult.RenderedReports);

        if (!options.Preview && !options.DryRun && pipelineResult.Succeeded)
        {
            await _stateStore.SaveAsync(
                new ReporterState
                {
                    SchemaVersion = ReporterState.CurrentSchemaVersion,
                    ReporterVersion = ReporterVersionInfo.Version,
                    LastSuccessfulRunAt = period.End,
                    LastReportHash = hash
                },
                cancellationToken).ConfigureAwait(false);
        }

        return new RunOutcome
        {
            Report = report,
            Pipeline = pipelineResult,
            Period = period,
            ReportHash = hash
        };
    }

    public static IReadOnlyList<IReportRenderer> BuildRenderers(ReporterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var renderers = new List<IReportRenderer>();
        if (configuration.Outputs.GitHubProfile.Enabled)
        {
            renderers.Add(new MarkdownReportRenderer());
        }

        if (configuration.Outputs.Json.Enabled)
        {
            renderers.Add(new JsonReportRenderer());
        }

        if (configuration.Outputs.Dashboard.Enabled)
        {
            renderers.Add(new SvgDashboardRenderer());
        }

        if (configuration.Outputs.Website.Enabled)
        {
            renderers.Add(new StaticHtmlReportRenderer());
        }

        if (configuration.Outputs.Email.Enabled)
        {
            renderers.Add(new EmailReportRenderer());
        }

        if (configuration.Outputs.Slack.Enabled)
        {
            renderers.Add(new SlackBlockKitRenderer());
        }

        return renderers;
    }

    private IPublicActivitySummarizer BuildSummarizer(ReporterConfiguration configuration)
    {
        var ruleBased = new RuleBasedPublicActivitySummarizer(configuration.Summary);
        if (!configuration.Privacy.Public.AiSummary)
        {
            return ruleBased;
        }

        var ai = configuration.Summary.Ai;
        var credential = Environment.GetEnvironmentVariable(ai.ApiKeySecretName);
        if (string.IsNullOrWhiteSpace(credential))
        {
            _log.Warning($"AI summary credential '{ai.ApiKeySecretName}' is not set; using rule-based summaries.");
            return ruleBased;
        }

        _privateTerms.Add(credential);
        IAiTextClient client = ai.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAiResponsesClient(credential, ai.Model, maxRetries: ai.MaxRetries),
            "github-copilot" => new GitHubCopilotClient(credential, ai.Model),
            "github-models" => throw new InvalidOperationException(
                "The 'github-models' provider is no longer supported. " +
                "Change summary.ai.provider to 'github-copilot'."),
            _ => throw new InvalidOperationException($"Unsupported AI summary provider '{ai.Provider}'.")
        };

        var primary = new AiPublicActivitySummarizer(client, configuration.Summary, configuration.Privacy.Public);
        return new FallbackPublicActivitySummarizer(
            primary,
            ruleBased,
            _log,
            TimeSpan.FromSeconds(ai.TimeoutSeconds));
    }

    private static IReadOnlyList<IReportPublisher> BuildPublishers(RunOptions options, IReporterLog log)
    {
        var publishers = new List<IReportPublisher>();

        if (options.Configuration.Publishers.Local.Enabled)
        {
            publishers.Add(new LocalFileReportPublisher(log));
        }

        if (options.Configuration.Publishers.GitHubProfile.Enabled)
        {
            publishers.Add(new GitHubProfileReportPublisher(log: log));
        }

        if (options.Configuration.Publishers.GitHubPages.Enabled)
        {
            publishers.Add(new GitHubPagesReportPublisher(log));
        }

        if (options.Configuration.Publishers.Email.Enabled)
        {
            publishers.Add(new EmailReportPublisher());
        }

        if (options.Configuration.Publishers.Slack.Enabled)
        {
            publishers.Add(new SlackReportPublisher());
        }

        return publishers;
    }

    private static IReadOnlyDictionary<string, string> BuildPublisherOptions(RunOptions options)
        => new Dictionary<string, string>
        {
            [GitHubProfilePublisherOptions.RepositoryPathOption] =
                options.ProfileRepositoryPath ?? options.WorkingDirectory,
            [GitHubProfilePublisherOptions.CommitOption] = options.CommitProfileRepository.ToString(),
            [GitHubProfilePublisherOptions.PushOption] = options.PushProfileRepository.ToString()
        };

    private static IReadOnlySet<string> BuildExcludedRepositories(ReporterConfiguration configuration)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var profileRepository = configuration.GitHub.ProfileRepository.FullName;
        if (!string.IsNullOrWhiteSpace(profileRepository))
        {
            excluded.Add(profileRepository.Trim());
        }

        return excluded;
    }
}
