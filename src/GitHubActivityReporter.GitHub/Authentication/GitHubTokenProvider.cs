namespace GitHubActivityReporter.GitHub.Authentication;

/// <summary>
/// Resolves the GitHub token from the environment. The value is never written to a
/// file, never logged and never stored in the configuration.
/// </summary>
public sealed class GitHubTokenProvider
{
    public static readonly string[] DefaultEnvironmentVariables =
    [
        "ACTIVITY_REPORTER_GITHUB_TOKEN",
        "GITHUB_TOKEN",
        "GH_TOKEN"
    ];

    private readonly Func<string, string?> _environmentReader;

    public GitHubTokenProvider(Func<string, string?>? environmentReader = null)
    {
        _environmentReader = environmentReader ?? Environment.GetEnvironmentVariable;
    }

    public string? TryGetToken(string? preferredVariable = null)
    {
        foreach (var candidate in EnumerateCandidateNames(preferredVariable))
        {
            var value = _environmentReader(candidate);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>Name of the environment variable that currently provides a token, if any.</summary>
    public string? FindTokenVariableName(string? preferredVariable = null)
    {
        return EnumerateCandidateNames(preferredVariable)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(_environmentReader(name)));
    }

    public IReadOnlyList<string> GetCandidateVariableNames(string? preferredVariable = null)
        => EnumerateCandidateNames(preferredVariable).ToArray();

    private static IEnumerable<string> EnumerateCandidateNames(string? preferredVariable)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredVariable))
        {
            candidates.Add(preferredVariable!);
        }

        candidates.AddRange(DefaultEnvironmentVariables);
        return candidates.Distinct(StringComparer.Ordinal);
    }
}
