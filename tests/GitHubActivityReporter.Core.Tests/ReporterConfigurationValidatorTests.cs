using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Core.Tests;

public sealed class ReporterConfigurationValidatorTests
{
    [Fact]
    public void Dashboard_and_website_outputs_can_be_enabled_with_phase2_renderers()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Dashboard.Enabled = true;
        config.Outputs.Dashboard.Renderer = KnownRenderers.SvgDashboard;
        config.Outputs.Dashboard.Target = "generated/activity-dashboard.svg";
        config.Outputs.Website.Enabled = true;
        config.Outputs.Website.Renderer = KnownRenderers.StaticHtml;
        config.Outputs.Website.OutputDirectory = "generated/site";
        config.Outputs.Website.HistoryDays = 30;

        var result = new ReporterConfigurationValidator().Validate(config);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Website_history_days_must_be_in_valid_range_when_website_output_is_enabled()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Outputs.Website.Enabled = true;
        config.Outputs.Website.HistoryDays = 0;

        var result = new ReporterConfigurationValidator().Validate(config);

        Assert.Contains(result.Errors, e => e.PropertyName == "Outputs.Website.HistoryDays");
    }

    [Fact]
    public void GitHub_pages_requires_the_static_website_output()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Publishers.GitHubPages.Enabled = true;

        var invalid = new ReporterConfigurationValidator().Validate(config);

        Assert.Contains(invalid.Errors, e => e.PropertyName == "Outputs.Website.Enabled");

        config.Outputs.Website.Enabled = true;
        var valid = new ReporterConfigurationValidator().Validate(config);

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Channel_publishers_require_their_outputs()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Publishers.Email.Enabled = true;
        config.Publishers.Slack.Enabled = true;

        var invalid = new ReporterConfigurationValidator().Validate(config);

        Assert.Contains(invalid.Errors, e => e.PropertyName == "Outputs.Email.Enabled");
        Assert.Contains(invalid.Errors, e => e.PropertyName == "Outputs.Slack.Enabled");

        config.Outputs.Email.Enabled = true;
        config.Outputs.Slack.Enabled = true;
        var valid = new ReporterConfigurationValidator().Validate(config);

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Public_ai_summary_accepts_bounded_supported_provider_configuration()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Privacy.Public.AiSummary = true;

        var valid = new ReporterConfigurationValidator().Validate(config);

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Errors.Select(e => e.ErrorMessage)));

        config.Summary.Ai.Provider = "unknown";
        config.Summary.Ai.MaxOutputTokens = 10_000;
        var invalid = new ReporterConfigurationValidator().Validate(config);

        Assert.Contains(invalid.Errors, e => e.PropertyName == "Summary.Ai.Provider");
        Assert.Contains(invalid.Errors, e => e.PropertyName == "Summary.Ai.MaxOutputTokens");
    }

    [Fact]
    public void Public_ai_summary_accepts_github_copilot_provider()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Privacy.Public.AiSummary = true;
        config.Summary.Ai.Provider = "github-copilot";
        config.Summary.Ai.Model = "auto";
        config.Summary.Ai.ApiKeySecretName = "GIT_ACCESS_TOKEN";

        var result = new ReporterConfigurationValidator().Validate(config);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Public_ai_summary_rejects_github_models_provider_with_migration_message()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Privacy.Public.AiSummary = true;
        config.Summary.Ai.Provider = "github-models";
        config.Summary.Ai.Model = "openai/gpt-4.1";

        var result = new ReporterConfigurationValidator().Validate(config);

        Assert.False(result.IsValid);
        var providerError = Assert.Single(result.Errors, e => e.PropertyName == "Summary.Ai.Provider");
        Assert.Contains("github-copilot", providerError.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_ai_summary_accepts_github_copilot_with_empty_model()
    {
        // github-copilot defaults model to 'auto' so empty is allowed.
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Privacy.Public.AiSummary = true;
        config.Summary.Ai.Provider = "github-copilot";
        config.Summary.Ai.Model = string.Empty;
        config.Summary.Ai.ApiKeySecretName = "GIT_ACCESS_TOKEN";

        var result = new ReporterConfigurationValidator().Validate(config);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Public_change_detail_level_must_be_supported()
    {
        var config = ReporterConfiguration.CreateDefault("example-user");
        config.Summary.PublicChangeDetailLevel = "deep";

        var invalid = new ReporterConfigurationValidator().Validate(config);

        Assert.Contains(invalid.Errors, e => e.PropertyName == "Summary.PublicChangeDetailLevel");
    }
}
