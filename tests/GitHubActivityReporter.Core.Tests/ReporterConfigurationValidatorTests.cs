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
}
