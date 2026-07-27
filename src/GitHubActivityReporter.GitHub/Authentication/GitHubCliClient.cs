namespace GitHubActivityReporter.GitHub.Authentication;

/// <summary>
/// Thin wrapper around the <c>gh</c> CLI used for authentication checks and
/// repository bootstrapping. Token values obtained here stay in memory only.
/// </summary>
public sealed class GitHubCliClient : IGitHubAuthenticationProbe
{
    private readonly IProcessRunner _processRunner;
    private readonly GitHubTokenProvider _tokenProvider;

    public GitHubCliClient(IProcessRunner? processRunner = null, GitHubTokenProvider? tokenProvider = null)
    {
        _processRunner = processRunner ?? new ProcessRunner();
        _tokenProvider = tokenProvider ?? new GitHubTokenProvider();
    }

    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync("gh", ["--version"], cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    public async Task<GitHubAuthenticationStatus> CheckAsync(
        string? tokenEnvironmentVariable,
        CancellationToken cancellationToken)
    {
        var variable = _tokenProvider.FindTokenVariableName(tokenEnvironmentVariable);
        if (variable is not null)
        {
            var login = await GetLoginAsync(cancellationToken).ConfigureAwait(false);
            return new GitHubAuthenticationStatus
            {
                IsAuthenticated = true,
                UserName = login,
                Source = $"environment:{variable}"
            };
        }

        var status = await _processRunner.RunAsync("gh", ["auth", "status"], cancellationToken).ConfigureAwait(false);
        if (!status.Succeeded)
        {
            return GitHubAuthenticationStatus.NotAuthenticated(
                "No GitHub credential found. Set a token environment variable or run 'gh auth login'.");
        }

        var cliLogin = await GetLoginAsync(cancellationToken).ConfigureAwait(false);
        return new GitHubAuthenticationStatus
        {
            IsAuthenticated = true,
            UserName = cliLogin,
            Source = "gh-cli"
        };
    }

    public async Task<string?> GetLoginAsync(CancellationToken cancellationToken)
    {
        var result = await _processRunner
            .RunAsync("gh", ["api", "user", "--jq", ".login"], cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }

    /// <summary>Reads the token from the environment or from the gh CLI. Never persisted.</summary>
    public async Task<string?> ResolveTokenAsync(string? tokenEnvironmentVariable, CancellationToken cancellationToken)
    {
        var token = _tokenProvider.TryGetToken(tokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        var result = await _processRunner.RunAsync("gh", ["auth", "token"], cancellationToken).ConfigureAwait(false);
        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }

    public async Task<bool> RepositoryExistsAsync(string owner, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var result = await _processRunner
            .RunAsync("gh", ["repo", "view", $"{owner}/{name}", "--json", "name"], cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded;
    }

    public async Task<bool> CreateProfileRepositoryAsync(string owner, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var result = await _processRunner
            .RunAsync("gh", ["repo", "create", $"{owner}/{name}", "--public", "--add-readme"], cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded;
    }

    public async Task<bool> SecretExistsAsync(string repository, string secretName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var result = await _processRunner
            .RunAsync("gh", ["secret", "list", "--repo", repository], cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
               && result.StandardOutput.Contains(secretName, StringComparison.Ordinal);
    }
}
