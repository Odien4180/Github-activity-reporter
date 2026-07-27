using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.GitHub.Authentication;
using GitHubActivityReporter.GitHub.Collectors;

namespace GitHubActivityReporter.Cli.Services;

public sealed record CollectorCreationResult
{
    public IActivityCollector? Collector { get; init; }

    public string? Error { get; init; }

    public string? TokenSource { get; init; }

    public bool Succeeded => Collector is not null;
}

/// <summary>Resolves a GitHub credential and creates the activity collector.</summary>
public sealed class CollectorFactory
{
    private readonly GitHubCliClient _cli;

    public CollectorFactory(GitHubCliClient? cli = null)
    {
        _cli = cli ?? new GitHubCliClient();
    }

    public async Task<CollectorCreationResult> CreateAsync(
        ReporterConfiguration configuration,
        IPrivateTermRegistry privateTerms,
        IReporterLog log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(privateTerms);

        var token = await _cli
            .ResolveTokenAsync(configuration.GitHub.TokenSecretName, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(token))
        {
            return new CollectorCreationResult
            {
                Error = $"No GitHub credential found. Export {configuration.GitHub.TokenSecretName} or run 'gh auth login'."
            };
        }

        return new CollectorCreationResult
        {
            Collector = GitHubActivityCollector.Create(token!, privateTerms, log),
            TokenSource = "resolved credential"
        };
    }
}
