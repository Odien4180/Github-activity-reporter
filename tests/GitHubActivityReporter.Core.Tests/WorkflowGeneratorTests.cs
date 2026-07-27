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
        Assert.Contains("--profile-path profile-repository --commit --push", workflow, StringComparison.Ordinal);
        Assert.Contains("git -C profile-repository config user.name", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("git push origin HEAD:master", workflow, StringComparison.Ordinal);
    }
}
