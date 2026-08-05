using GitHubActivityReporter.Bootstrap.Templates;
using GitHubActivityReporter.Cli.Services;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.Core.State;
using GitHubActivityReporter.Core.Validation;
using GitHubActivityReporter.GitHub.Authentication;
using GitHubActivityReporter.Publishing.GitHubProfile;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GitHubActivityReporter.Cli.Commands;

public enum CheckStatus
{
    Ok,
    Warning,
    Failed
}

public sealed record CheckResult(CheckStatus Status, string Message);

/// <summary>Diagnoses the local setup without publishing anything.</summary>
public sealed class DoctorCommand : AsyncCommand<ReporterSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ReporterSettings settings, CancellationToken cancellationToken)
    {
        var workingDirectory = settings.ResolveWorkingDirectory();
        var checks = new List<CheckResult>();
        var loader = new ReporterContextLoader();
        LoadedConfiguration? loaded = null;

        try
        {
            loaded = await loader
                .LoadAsync(settings.ConfigPath, workingDirectory, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            checks.Add(new CheckResult(CheckStatus.Ok, $"Configuration found and valid ({loaded.Path})"));
        }
        catch (ConfigurationLoadException exception)
        {
            checks.Add(new CheckResult(CheckStatus.Failed, exception.Message));
            foreach (var error in exception.Errors)
            {
                checks.Add(new CheckResult(CheckStatus.Failed, error));
            }
        }

        var cli = new GitHubCliClient();
        var cliInstalled = await cli.IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        checks.Add(cliInstalled
            ? new CheckResult(CheckStatus.Ok, "GitHub CLI is installed")
            : new CheckResult(CheckStatus.Warning, "GitHub CLI (gh) is not installed. A token environment variable is then required."));

        var secretName = loaded?.Configuration.GitHub.TokenSecretName ?? "ACTIVITY_REPORTER_GITHUB_TOKEN";
        var authentication = await cli.CheckAsync(secretName, cancellationToken).ConfigureAwait(false);
        checks.Add(authentication.IsAuthenticated
            ? new CheckResult(CheckStatus.Ok, $"GitHub authenticated ({authentication.Source})")
            : new CheckResult(CheckStatus.Failed, authentication.Message ?? "GitHub authentication failed."));
        foreach (var diagnostic in authentication.Diagnostics)
        {
            checks.Add(new CheckResult(authentication.IsAuthenticated ? CheckStatus.Ok : CheckStatus.Warning, diagnostic));
        }

        if (loaded is not null)
        {
            checks.AddRange(await CheckConfiguredEnvironmentAsync(loaded, workingDirectory, cli, authentication).ConfigureAwait(false));
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Status");
        table.AddColumn("Check");

        foreach (var check in checks)
        {
            var (icon, color) = check.Status switch
            {
                CheckStatus.Ok => ("✓", "green"),
                CheckStatus.Warning => ("⚠", "yellow"),
                _ => ("✗", "red")
            };

            table.AddRow($"[{color}]{icon}[/]", Markup.Escape(check.Message));
        }

        AnsiConsole.Write(table);
        return checks.Any(c => c.Status == CheckStatus.Failed) ? 1 : 0;
    }

    private static async Task<IReadOnlyList<CheckResult>> CheckConfiguredEnvironmentAsync(
        LoadedConfiguration loaded,
        string workingDirectory,
        GitHubCliClient cli,
        GitHubAuthenticationStatus authentication)
    {
        var configuration = loaded.Configuration;
        var checks = new List<CheckResult>();

        if (authentication.IsAuthenticated && !string.IsNullOrWhiteSpace(authentication.UserName))
        {
            checks.Add(string.Equals(authentication.UserName, configuration.GitHub.Username, StringComparison.OrdinalIgnoreCase)
                ? new CheckResult(CheckStatus.Ok, $"Authenticated user matches github.username ({configuration.GitHub.Username})")
                : new CheckResult(CheckStatus.Warning, "The authenticated user does not match github.username."));
        }

        var repository = configuration.GitHub.ProfileRepository;
        if (await cli.IsInstalledAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var exists = await cli
                .RepositoryExistsAsync(repository.Owner, repository.Name, CancellationToken.None)
                .ConfigureAwait(false);

            checks.Add(exists
                ? new CheckResult(CheckStatus.Ok, $"Profile repository found ({repository.FullName})")
                : new CheckResult(CheckStatus.Warning, $"Profile repository {repository.FullName} was not found or is not readable."));

            var secretExists = await cli
                .SecretExistsAsync(repository.FullName, configuration.GitHub.TokenSecretName, CancellationToken.None)
                .ConfigureAwait(false);

            checks.Add(secretExists
                ? new CheckResult(CheckStatus.Ok, $"Actions secret {configuration.GitHub.TokenSecretName} is configured")
                : new CheckResult(CheckStatus.Warning,
                    $"Actions secret {configuration.GitHub.TokenSecretName} was not found. Run: gh secret set {configuration.GitHub.TokenSecretName}"));
        }

        var readmePath = Path.Combine(workingDirectory, "README.md");
        if (File.Exists(readmePath))
        {
            var readme = await File.ReadAllTextAsync(readmePath).ConfigureAwait(false);
            try
            {
                ReadmeMarkerUpdater.Update(readme, "probe");
                checks.Add(new CheckResult(CheckStatus.Ok, ReadmeMarkerUpdater.HasMarkers(readme)
                    ? "README markers are valid"
                    : "README has no markers yet, the generated block will be appended"));
            }
            catch (ReadmeMarkerException exception)
            {
                checks.Add(new CheckResult(CheckStatus.Failed, exception.Message));
            }
        }
        else
        {
            checks.Add(new CheckResult(CheckStatus.Warning, "No README.md found in the working directory."));
        }

        checks.Add(CheckWritable(workingDirectory, configuration));

        var stateStore = new FileReporterStateStore(workingDirectory);
        var state = await stateStore.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        checks.Add(state is null
            ? new CheckResult(CheckStatus.Warning, "No previous successful run recorded yet.")
            : new CheckResult(CheckStatus.Ok, $"State file valid, last successful run {state.LastSuccessfulRunAt:u}"));

        var workflowPath = Path.Combine(workingDirectory, WorkflowTemplate.RelativeDirectory, WorkflowTemplate.FileName);
        checks.Add(File.Exists(workflowPath)
            ? new CheckResult(CheckStatus.Ok, "GitHub Actions workflow is installed")
            : new CheckResult(CheckStatus.Warning, "Workflow not installed. Run: github-activity-reporter install-workflow"));

        checks.AddRange(CheckRendererCompatibility(configuration));
        checks.AddRange(await CheckGeneratedOutputsAsync(configuration, workingDirectory).ConfigureAwait(false));

        return checks;
    }

    private static CheckResult CheckWritable(string workingDirectory, ReporterConfiguration configuration)
    {
        var directory = Path.Combine(workingDirectory, configuration.Publishers.Local.OutputDirectory);
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write-probe");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return new CheckResult(CheckStatus.Ok, "Local output directory is writable");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CheckResult(CheckStatus.Failed, "Local output directory is not writable.");
        }
    }

    private static IEnumerable<CheckResult> CheckRendererCompatibility(ReporterConfiguration configuration)
    {
        if (configuration.Outputs.GitHubProfile.Enabled && !configuration.Publishers.GitHubProfile.Enabled
            && !configuration.Publishers.Local.Enabled)
        {
            yield return new CheckResult(CheckStatus.Warning,
                "The markdown output is enabled but no publisher is enabled for it.");
        }

        if (configuration.Publishers.GitHubProfile.Enabled && !configuration.Outputs.GitHubProfile.Enabled)
        {
            yield return new CheckResult(CheckStatus.Warning,
                "The GitHub profile publisher is enabled but the markdown output is disabled.");
        }

        if (configuration.Publishers.Email.Enabled)
        {
            yield return CheckSecret(
                configuration.Publishers.Email.SecretName,
                "Email credentials");
        }

        if (configuration.Publishers.Slack.Enabled)
        {
            yield return CheckSecret(
                configuration.Publishers.Slack.SecretName,
                "Slack webhook");
        }

        if (configuration.Privacy.Public.AiSummary)
        {
            yield return CheckSecret(
                configuration.Summary.Ai.ApiKeySecretName,
                $"AI summary provider ({configuration.Summary.Ai.Provider}) credential");
        }
    }

    private static CheckResult CheckSecret(string secretName, string description)
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(secretName))
            ? new CheckResult(CheckStatus.Warning, $"{description} is enabled but environment variable {secretName} is not set.")
            : new CheckResult(CheckStatus.Ok, $"{description} environment variable is set");

    private static async Task<IReadOnlyList<CheckResult>> CheckGeneratedOutputsAsync(
        ReporterConfiguration configuration,
        string workingDirectory)
    {
        var validator = new PrivacyValidator();
        var validationContext = ValidationContext.Create(new InMemoryPrivateTermRegistry(), configuration);
        var results = new List<CheckResult>();

        var targets = new List<string>();
        if (configuration.Outputs.GitHubProfile.Enabled)
        {
            targets.Add(configuration.Outputs.GitHubProfile.Target);
        }

        if (configuration.Outputs.Json.Enabled)
        {
            targets.Add(configuration.Outputs.Json.Target);
        }

        if (configuration.Outputs.Dashboard.Enabled)
        {
            targets.Add(configuration.Outputs.Dashboard.Target);
        }

        if (configuration.Outputs.Website.Enabled)
        {
            targets.Add(Path.Combine(configuration.Outputs.Website.OutputDirectory, "index.html"));
        }

        if (configuration.Outputs.Email.Enabled)
        {
            targets.Add(configuration.Outputs.Email.HtmlTarget);
            targets.Add(configuration.Outputs.Email.TextTarget);
        }

        if (configuration.Outputs.Slack.Enabled)
        {
            targets.Add(configuration.Outputs.Slack.Target);
        }

        foreach (var target in targets)
        {
            var path = Path.Combine(workingDirectory, target);
            if (!File.Exists(path))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var result = validator.ValidateContent(content, target, validationContext);

            results.Add(result.IsValid
                ? new CheckResult(CheckStatus.Ok, $"Privacy validation passed for {target}")
                : new CheckResult(CheckStatus.Failed, $"Privacy validation failed for {target}"));
        }

        return results;
    }
}
