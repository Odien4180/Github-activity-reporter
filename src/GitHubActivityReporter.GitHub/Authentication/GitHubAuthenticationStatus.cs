namespace GitHubActivityReporter.GitHub.Authentication;

public sealed record GitHubAuthenticationStatus
{
    public required bool IsAuthenticated { get; init; }

    public string? UserName { get; init; }

    /// <summary>How the credential was obtained, e.g. <c>environment:GITHUB_TOKEN</c> or <c>gh-cli</c>.</summary>
    public string? Source { get; init; }

    /// <summary>Safe, token free description of the failure.</summary>
    public string? Message { get; init; }

    /// <summary>Safe diagnostic details for logs and doctor output.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();

    public static GitHubAuthenticationStatus NotAuthenticated(string message, IReadOnlyList<string>? diagnostics = null)
        => new() { IsAuthenticated = false, Message = message, Diagnostics = diagnostics ?? Array.Empty<string>() };
}

public interface IGitHubAuthenticationProbe
{
    Task<GitHubAuthenticationStatus> CheckAsync(string? tokenEnvironmentVariable, CancellationToken cancellationToken);
}
