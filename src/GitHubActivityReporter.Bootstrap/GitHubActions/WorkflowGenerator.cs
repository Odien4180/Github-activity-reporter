using GitHubActivityReporter.Bootstrap.Templates;
using GitHubActivityReporter.Core.Configuration;
using Scriban;
using Scriban.Runtime;

namespace GitHubActivityReporter.Bootstrap.GitHubActions;

public sealed record WorkflowOptions
{
    public string WorkflowName { get; init; } = "Update GitHub activity report";

    public string DotnetVersion { get; init; } = "10.0.x";

    public string SolutionPath { get; init; } = "GitHubActivityReporter.sln";

    public string CliProjectPath { get; init; } = "src/GitHubActivityReporter.Cli";

    public string ConfigPath { get; init; } = ReporterConfiguration.DefaultFileName;

    public string ProfileRepositoryPath { get; init; } = "profile-repository";

    public string CommitMessage { get; init; } = "chore(profile): update GitHub activity report";
}

/// <summary>Generates <c>.github/workflows/update-activity-report.yml</c>.</summary>
public sealed class WorkflowGenerator
{
    public string Generate(ReporterConfiguration configuration, WorkflowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        options ??= new WorkflowOptions();

        var cron = CronExpressionGenerator.Generate(configuration.Schedule);

        var template = Template.Parse(WorkflowTemplate.Scriban);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                "The workflow template is invalid: " + string.Join("; ", template.Messages.Select(m => m.Message)));
        }

        var model = new ScriptObject
        {
            ["workflow_name"] = options.WorkflowName,
            ["cron"] = cron,
            ["schedule_description"] = CronExpressionGenerator.Describe(configuration.Schedule),
            ["dotnet_version"] = options.DotnetVersion,
            ["solution_path"] = options.SolutionPath,
            ["cli_project_path"] = options.CliProjectPath,
            ["config_path"] = options.ConfigPath,
            ["profile_repository"] = configuration.GitHub.ProfileRepository.FullName,
            ["profile_repository_path"] = options.ProfileRepositoryPath,
            ["token_secret_name"] = configuration.GitHub.TokenSecretName,
            ["commit_message"] = options.CommitMessage,
            ["branch"] = configuration.GitHub.ProfileRepository.Branch
        };

        var context = new TemplateContext { StrictVariables = true };
        context.PushGlobal(model);

        var rendered = template.Render(context);
        return rendered.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
    }
}
