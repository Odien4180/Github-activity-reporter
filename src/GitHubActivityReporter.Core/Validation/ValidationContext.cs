using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Core.Validation;

/// <summary>Input for <see cref="IOutputValidator"/> implementations.</summary>
public sealed record ValidationContext
{
    /// <summary>Strings that originate from private repositories and must never appear in output.</summary>
    public IReadOnlyCollection<string> ForbiddenTerms { get; init; } = Array.Empty<string>();

    /// <summary>Known secret values (token values, webhook urls) that must never appear in output.</summary>
    public IReadOnlyCollection<string> SecretValues { get; init; } = Array.Empty<string>();

    public bool DetectEmailAddresses { get; init; } = true;

    public bool DetectFullCommitHashes { get; init; } = true;

    public bool DetectTokens { get; init; } = true;

    /// <summary>Optional label describing where the artifacts came from.</summary>
    public string? Origin { get; init; }

    public static ValidationContext Create(
        IPrivateTermRegistry registry,
        ReporterConfiguration? configuration = null,
        IEnumerable<string>? secretValues = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var terms = new List<string>(registry.Terms);
        if (configuration is not null)
        {
            terms.AddRange(configuration.Privacy.CustomForbiddenTerms);
        }

        // The configured GitHub username appears legitimately in report outputs
        // (profile links, attribution, etc.) so it must not be treated as a forbidden term.
        var allowedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(configuration?.GitHub.Username))
        {
            allowedTerms.Add(configuration.GitHub.Username.Trim());
        }

        return new ValidationContext
        {
            ForbiddenTerms = terms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => !allowedTerms.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SecretValues = (secretValues ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}
