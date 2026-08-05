using System.Globalization;
using FluentValidation;

namespace GitHubActivityReporter.Core.Configuration;

/// <summary>Validates a loaded configuration before it is used by any command.</summary>
public sealed class ReporterConfigurationValidator : AbstractValidator<ReporterConfiguration>
{
    public ReporterConfigurationValidator()
    {
        RuleFor(c => c.Version)
            .Equal(ReporterConfiguration.CurrentVersion)
            .WithMessage($"Only configuration version {ReporterConfiguration.CurrentVersion} is supported.");

        RuleFor(c => c.GitHub.Username)
            .NotEmpty().WithMessage("github.username is required.")
            .Matches("^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$")
            .WithMessage("github.username is not a valid GitHub login.");

        RuleFor(c => c.GitHub.ProfileRepository.Owner)
            .NotEmpty().WithMessage("github.profile_repository.owner is required.");

        RuleFor(c => c.GitHub.ProfileRepository.Name)
            .NotEmpty().WithMessage("github.profile_repository.name is required.");

        RuleFor(c => c.GitHub.ProfileRepository.Branch)
            .NotEmpty().WithMessage("github.profile_repository.branch is required.");

        RuleFor(c => c.GitHub.TokenSecretName)
            .NotEmpty().WithMessage("github.token_secret_name is required.")
            .Matches("^[A-Z][A-Z0-9_]*$")
            .WithMessage("github.token_secret_name must be an uppercase secret name, not a token value.");

        RuleFor(c => c.Collection.Period.InitialLookback)
            .Must(v => DurationParser.TryParse(v, out _))
            .WithMessage("collection.period.initial_lookback must be a duration such as 24h or 7d.");

        RuleFor(c => c.Collection.Period.CustomLookback)
            .Must(v => DurationParser.TryParse(v, out _))
            .When(c => c.Collection.Period.Mode == PeriodMode.Custom)
            .WithMessage("collection.period.custom_lookback is required when mode is custom.");

        RuleFor(c => c.Collection)
            .Must(c => c.Public.Enabled || c.Private.Enabled)
            .WithMessage("At least one of collection.public.enabled or collection.private.enabled must be true.");

        RuleFor(c => c.Privacy.Private.Mode)
            .Equal(PrivateExposureMode.AggregateOnly)
            .WithMessage("privacy.private.mode must be aggregate-only.");

        RuleFor(c => c.Privacy.Private)
            .Must(p => !p.ExposeRepositoryNames
                       && !p.ExposeRepositoryAliases
                       && !p.ExposeOrganizationNames
                       && !p.ExposeTitles
                       && !p.ExposeLinks
                       && !p.ExposeCommitMessages
                       && !p.ExposeBranchNames
                       && !p.ExposeFilePaths
                       && !p.ExposeTopics)
            .WithMessage("privacy.private may only expose aggregate counters. Identifying fields cannot be enabled.");

        RuleFor(c => c.Privacy.Private.AiSummary)
            .Equal(false)
            .WithMessage("privacy.private.ai_summary must be false. Private activity is never sent to a summarizer.");

        RuleFor(c => c.Summary.Ai.Provider)
            .Must(provider => provider is "openai" or "github-copilot")
            .When(c => c.Privacy.Public.AiSummary)
            .WithMessage("summary.ai.provider must be 'openai' or 'github-copilot'. " +
                         "If you are migrating from 'github-models', change provider to 'github-copilot' and model to 'auto'.");

        RuleFor(c => c.Summary.Ai.Model)
            .NotEmpty()
            .When(c => c.Privacy.Public.AiSummary && c.Summary.Ai.Provider != "github-copilot")
            .WithMessage("summary.ai.model is required when public AI summary is enabled.");

        RuleFor(c => c.Summary.Ai.ApiKeySecretName)
            .NotEmpty()
            .Matches("^[A-Z][A-Z0-9_]*$")
            .When(c => c.Privacy.Public.AiSummary)
            .WithMessage("summary.ai.api_key_secret_name must be an uppercase environment variable name.");

        RuleFor(c => c.Summary.Ai.MaxInputEvents)
            .InclusiveBetween(1, 500)
            .When(c => c.Privacy.Public.AiSummary);

        RuleFor(c => c.Summary.Ai.MaxInputCharacters)
            .InclusiveBetween(100, 100_000)
            .When(c => c.Privacy.Public.AiSummary);

        RuleFor(c => c.Summary.Ai.MaxOutputTokens)
            .InclusiveBetween(64, 4_096)
            .When(c => c.Privacy.Public.AiSummary);

        RuleFor(c => c.Summary.Ai.TimeoutSeconds)
            .InclusiveBetween(1, 300)
            .When(c => c.Privacy.Public.AiSummary);

        RuleFor(c => c.Summary.Ai.MaxRetries)
            .InclusiveBetween(0, 5)
            .When(c => c.Privacy.Public.AiSummary);

        RuleFor(c => c.Summary.Language)
            .Must(l => l is "ko" or "en")
            .WithMessage("summary.language must be 'ko' or 'en'.");

        RuleFor(c => c.Summary.PublicChangeDetailLevel)
            .Must(level => level is "compact" or "standard" or "detailed")
            .WithMessage("summary.public_change_detail_level must be 'compact', 'standard', or 'detailed'.");

        RuleFor(c => c.Summary.MaxPublicRepositories)
            .InclusiveBetween(1, 50)
            .WithMessage("summary.max_public_repositories must be between 1 and 50.");

        RuleFor(c => c.Summary.MaxItemsPerRepository)
            .InclusiveBetween(1, 20)
            .WithMessage("summary.max_items_per_repository must be between 1 and 20.");

        RuleFor(c => c.Outputs)
            .Must(o => o.GitHubProfile.Enabled || o.Json.Enabled || o.Dashboard.Enabled || o.Website.Enabled || o.Email.Enabled || o.Slack.Enabled)
            .WithMessage("At least one output must be enabled.");

        RuleFor(c => c.Outputs.GitHubProfile)
            .Must(o => KnownRenderers.Implemented.Contains(o.Renderer))
            .When(c => c.Outputs.GitHubProfile.Enabled)
            .WithMessage($"outputs.github_profile.renderer must be '{KnownRenderers.CompactMarkdown}'.");

        RuleFor(c => c.Outputs.GitHubProfile.Target)
            .NotEmpty()
            .When(c => c.Outputs.GitHubProfile.Enabled)
            .WithMessage("outputs.github_profile.target is required.");

        RuleFor(c => c.Outputs.Json)
            .Must(o => KnownRenderers.Implemented.Contains(o.Renderer))
            .When(c => c.Outputs.Json.Enabled)
            .WithMessage($"outputs.json.renderer must be '{KnownRenderers.NormalizedJson}'.");

        RuleFor(c => c.Outputs.Json.Target)
            .NotEmpty()
            .When(c => c.Outputs.Json.Enabled)
            .WithMessage("outputs.json.target is required.");

        RuleFor(c => c.Outputs.Dashboard)
            .Must(o => KnownRenderers.Implemented.Contains(o.Renderer))
            .When(c => c.Outputs.Dashboard.Enabled)
            .WithMessage($"outputs.dashboard.renderer must be '{KnownRenderers.SvgDashboard}'.");

        RuleFor(c => c.Outputs.Dashboard.Target)
            .NotEmpty()
            .When(c => c.Outputs.Dashboard.Enabled)
            .WithMessage("outputs.dashboard.target is required.");

        RuleFor(c => c.Outputs.Website)
            .Must(o => KnownRenderers.Implemented.Contains(o.Renderer))
            .When(c => c.Outputs.Website.Enabled)
            .WithMessage($"outputs.website.renderer must be '{KnownRenderers.StaticHtml}'.");

        RuleFor(c => c.Outputs.Website.OutputDirectory)
            .NotEmpty()
            .When(c => c.Outputs.Website.Enabled)
            .WithMessage("outputs.website.output_directory is required.");

        RuleFor(c => c.Outputs.Website.HistoryDays)
            .InclusiveBetween(1, 365)
            .When(c => c.Outputs.Website.Enabled)
            .WithMessage("outputs.website.history_days must be between 1 and 365.");

        RuleFor(c => c.Outputs.Email.Renderer)
            .Equal(KnownRenderers.EmailHtml)
            .When(c => c.Outputs.Email.Enabled)
            .WithMessage($"outputs.email.renderer must be '{KnownRenderers.EmailHtml}'.");

        RuleFor(c => c.Outputs.Email.HtmlTarget)
            .NotEmpty()
            .When(c => c.Outputs.Email.Enabled)
            .WithMessage("outputs.email.html_target is required.");

        RuleFor(c => c.Outputs.Email.TextTarget)
            .NotEmpty()
            .When(c => c.Outputs.Email.Enabled)
            .WithMessage("outputs.email.text_target is required.");

        RuleFor(c => c.Outputs.Slack.Renderer)
            .Equal(KnownRenderers.SlackBlocks)
            .When(c => c.Outputs.Slack.Enabled)
            .WithMessage($"outputs.slack.renderer must be '{KnownRenderers.SlackBlocks}'.");

        RuleFor(c => c.Outputs.Slack.Target)
            .NotEmpty()
            .When(c => c.Outputs.Slack.Enabled)
            .WithMessage("outputs.slack.target is required.");

        RuleFor(c => c.Outputs.Website.Enabled)
            .Equal(true)
            .When(c => c.Publishers.GitHubPages.Enabled)
            .WithMessage("outputs.website.enabled must be true when the GitHub Pages publisher is enabled.");

        RuleFor(c => c.Publishers.GitHubPages.OutputDirectory)
            .NotEmpty()
            .When(c => c.Publishers.GitHubPages.Enabled)
            .WithMessage("publishers.github_pages.output_directory is required.");

        RuleFor(c => c.Outputs.Email.Enabled)
            .Equal(true)
            .When(c => c.Publishers.Email.Enabled)
            .WithMessage("outputs.email.enabled must be true when the email publisher is enabled.");

        RuleFor(c => c.Outputs.Slack.Enabled)
            .Equal(true)
            .When(c => c.Publishers.Slack.Enabled)
            .WithMessage("outputs.slack.enabled must be true when the Slack publisher is enabled.");

        RuleFor(c => c.Publishers.Local.OutputDirectory)
            .NotEmpty()
            .When(c => c.Publishers.Local.Enabled)
            .WithMessage("publishers.local.output_directory is required.");

        RuleFor(c => c.Publishers.Email.SecretName)
            .NotEmpty()
            .When(c => c.Publishers.Email.Enabled)
            .WithMessage("publishers.email.secret_name is required.");

        RuleFor(c => c.Publishers.Slack.SecretName)
            .NotEmpty()
            .When(c => c.Publishers.Slack.Enabled)
            .WithMessage("publishers.slack.secret_name is required.");

        RuleFor(c => c.Schedule.Timezone)
            .Must(BeAKnownTimeZone)
            .When(c => c.Schedule.Enabled)
            .WithMessage("schedule.timezone must be a valid IANA time zone such as Asia/Seoul.");

        RuleFor(c => c.Schedule.LocalTime)
            .Must(BeALocalTime)
            .When(c => c.Schedule.Enabled && c.Schedule.Frequency != ScheduleFrequency.Manual)
            .WithMessage("schedule.local_time must use the HH:mm format.");
    }

    private static bool BeAKnownTimeZone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool BeALocalTime(string value)
        => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
