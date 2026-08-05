using GitHubActivityReporter.Bootstrap.GitHubActions;
using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Core.Tests;

public sealed class WorkflowGeneratorTests
{
    [Fact]
    public void Generate_ChecksOutAndPublishesToConfiguredProfileRepository()
    {
        var configuration = ReporterConfiguration.CreateDefault("Odien4180");
        configuration.GitHub.ProfileRepository.Branch = "master";

        var workflow = new WorkflowGenerator().Generate(configuration);

        Assert.Contains("repository: Odien4180/Odien4180", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: master", workflow, StringComparison.Ordinal);
        Assert.Contains("path: profile-repository", workflow, StringComparison.Ordinal);
        Assert.Contains("--profile-path profile-repository --verbose", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--commit --push", workflow, StringComparison.Ordinal);
        Assert.Contains("git -C profile-repository config user.name", workflow, StringComparison.Ordinal);
        Assert.Contains("git -C profile-repository status --porcelain", workflow, StringComparison.Ordinal);
        Assert.Contains("git -C profile-repository push origin HEAD:master", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/checkout@v5", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-dotnet@v5", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/deploy-pages", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_adds_official_pages_deployment_when_enabled()
    {
        var configuration = ReporterConfiguration.CreateDefault("example-user");
        configuration.Outputs.Website.Enabled = true;
        configuration.Publishers.GitHubPages.Enabled = true;
        configuration.Publishers.GitHubPages.OutputDirectory = "artifacts/pages";

        var workflow = new WorkflowGenerator().Generate(configuration);

        Assert.Contains("pages: write", workflow, StringComparison.Ordinal);
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/upload-pages-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("path: artifacts/pages", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/deploy-pages@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("deploy-pages:\n    needs: update-activity-report", workflow, StringComparison.Ordinal);
        Assert.Contains("name: github-pages", workflow, StringComparison.Ordinal);
        Assert.Contains("url: ${{ steps.deployment.outputs.page_url }}", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "uses: actions/upload-pages-artifact@v4"));
        Assert.Equal(1, CountOccurrences(workflow, "uses: actions/deploy-pages@v4"));
    }

    [Fact]
    public void Generate_maps_channel_and_ai_secrets_without_embedding_values()
    {
        var configuration = ReporterConfiguration.CreateDefault("example-user");
        configuration.Outputs.Email.Enabled = true;
        configuration.Outputs.Slack.Enabled = true;
        configuration.Publishers.Email.Enabled = true;
        configuration.Publishers.Slack.Enabled = true;
        configuration.Privacy.Public.AiSummary = true;
        configuration.Summary.Ai.Provider = "github-copilot";
        configuration.Summary.Ai.Model = "auto";
        configuration.Summary.Ai.ApiKeySecretName = "GIT_ACCESS_TOKEN";

        var workflow = new WorkflowGenerator().Generate(configuration);

        Assert.DoesNotContain("models: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("COPILOT_GITHUB_TOKEN", workflow, StringComparison.Ordinal);
        Assert.Contains("GIT_ACCESS_TOKEN: ${{ secrets.GIT_ACCESS_TOKEN }}", workflow, StringComparison.Ordinal);
        Assert.Contains("EMAIL_CREDENTIALS: ${{ secrets.EMAIL_CREDENTIALS }}", workflow, StringComparison.Ordinal);
        Assert.Contains("SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_does_not_emit_models_read_permission()
    {
        var configuration = ReporterConfiguration.CreateDefault("example-user");
        configuration.Privacy.Public.AiSummary = true;
        configuration.Summary.Ai.Provider = "github-copilot";
        configuration.Summary.Ai.Model = "auto";
        configuration.Summary.Ai.ApiKeySecretName = "GIT_ACCESS_TOKEN";

        var workflow = new WorkflowGenerator().Generate(configuration);

        Assert.DoesNotContain("models: read", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_uses_github_copilot_config_path_when_specified()
    {
        var configuration = ReporterConfiguration.CreateDefault("example-user");
        var options = new WorkflowOptions { ConfigPath = "activity-reporter.github-copilot.yml" };

        var workflow = new WorkflowGenerator().Generate(configuration, options);

        Assert.Contains("activity-reporter.github-copilot.yml", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("activity-reporter.github-models.yml", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Installer_uses_the_selected_repository_configuration_path()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var configPath = Path.Combine(root, "config", "activity-reporter.github-copilot.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(configPath, "version: 1");

            var result = await new GitHubActivityReporter.Bootstrap.Generators.WorkflowInstaller().InstallAsync(
                ReporterConfiguration.CreateDefault("example-user"),
                root,
                configurationPath: configPath);

            Assert.Contains(
                "--config config/activity-reporter.github-copilot.yml",
                result.Content,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Installer_rejects_a_configuration_outside_the_workflow_repository()
    {
        var root = CreateTemporaryDirectory();
        var outside = Path.Combine(Path.GetTempPath(), $"activity-reporter-outside-{Guid.NewGuid():N}.yml");
        try
        {
            await File.WriteAllTextAsync(outside, "version: 1");

            var exception = await Assert.ThrowsAsync<IOException>(() =>
                new GitHubActivityReporter.Bootstrap.Generators.WorkflowInstaller().InstallAsync(
                    ReporterConfiguration.CreateDefault("example-user"),
                    root,
                    configurationPath: outside));

            Assert.Contains("must be inside the repository", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(outside);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"activity-reporter-workflow-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int CountOccurrences(string value, string search)
        => value.Split(search, StringSplitOptions.None).Length - 1;
}
