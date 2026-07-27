using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Bootstrap.ConfigurationSetup;

/// <summary>Builds and writes <c>activity-reporter.yml</c> from the init answers.</summary>
public sealed class ConfigurationScaffolder
{
    private readonly ConfigurationLoader _loader;

    public ConfigurationScaffolder(ConfigurationLoader? loader = null)
    {
        _loader = loader ?? ConfigurationLoader.Default;
    }

    public ReporterConfiguration Build(InitAnswers answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentException.ThrowIfNullOrWhiteSpace(answers.UserName);

        var configuration = new ReporterConfiguration
        {
            GitHub = new GitHubSettings
            {
                Username = answers.UserName,
                ProfileRepository = new ProfileRepositorySettings
                {
                    Owner = string.IsNullOrWhiteSpace(answers.ProfileRepositoryOwner)
                        ? answers.UserName
                        : answers.ProfileRepositoryOwner!,
                    Name = string.IsNullOrWhiteSpace(answers.ProfileRepositoryName)
                        ? answers.UserName
                        : answers.ProfileRepositoryName!,
                    Branch = string.IsNullOrWhiteSpace(answers.Branch) ? "main" : answers.Branch
                }
            },
            Collection = new CollectionSettings
            {
                Period = new PeriodSettings
                {
                    Mode = answers.PeriodMode,
                    InitialLookback = answers.InitialLookback
                },
                Public = new ActivitySourceSettings
                {
                    Enabled = answers.CollectPublic,
                    EventTypes = answers.PublicEventTypes
                },
                Private = new ActivitySourceSettings
                {
                    Enabled = answers.CollectPrivate,
                    EventTypes = answers.PrivateEventTypes
                }
            },
            Privacy = new PrivacySettings
            {
                Public = answers.PublicPrivacy,
                Private = answers.PrivatePrivacy
            },
            Summary = new SummarySettings
            {
                Language = answers.Language,
                Style = "concise",
                MaxPublicRepositories = answers.MaxPublicRepositories,
                MaxItemsPerRepository = answers.MaxItemsPerRepository
            },
            Schedule = new ScheduleSettings
            {
                Enabled = answers.Frequency != ScheduleFrequency.Manual,
                Timezone = answers.Timezone,
                LocalTime = answers.LocalTime,
                Frequency = answers.Frequency
            }
        };

        configuration.Outputs.GitHubProfile.Enabled = answers.MarkdownOutput;
        configuration.Outputs.GitHubProfile.Renderer = KnownRenderers.CompactMarkdown;
        configuration.Outputs.Json.Enabled = answers.JsonOutput;
        configuration.Outputs.Json.Renderer = KnownRenderers.NormalizedJson;

        configuration.Publishers.GitHubProfile.Enabled = answers.PublishToProfileRepository;
        configuration.Publishers.Local.Enabled = answers.PublishToLocalDirectory;
        configuration.Publishers.Local.OutputDirectory = answers.LocalOutputDirectory;

        return configuration;
    }

    public string Serialize(ReporterConfiguration configuration) => _loader.Serialize(configuration);

    public async Task<string> WriteAsync(
        ReporterConfiguration configuration,
        string path,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path) && !overwrite)
        {
            throw new IOException($"Configuration file already exists: {path}. Use --force to overwrite it.");
        }

        await _loader.SaveAsync(configuration, path, cancellationToken).ConfigureAwait(false);
        return Path.GetFullPath(path);
    }
}
