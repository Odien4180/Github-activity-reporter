namespace GitHubActivityReporter.GitHub.Authentication;

public sealed record GitHubAuthenticationStatus
{
    public required bool IsAuthenticated { get; init; }

    public string? UserName { get; init; }

    /// <summary>How the credential was obtained, e.g. <c>environment:GITHUB_TOKEN</c> or <c>gh-cli</c>.</summary>
    public string? Source { get; init; }

    /// <summary>Safe, token free description of the failure.</summary>
    public string? Message { get; init; }

    public static GitHubAuthenticationStatus NotAuthenticated(string message)
        => new() { IsAuthenticated = false, Message = message };
}

public interface IGitHubAuthenticationProbe
{
    Task<GitHubAuthenticationStatus> CheckAsync(string? tokenEnvironmentVariable, CancellationToken cancellationToken);
}
