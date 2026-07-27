using System.Text.RegularExpressions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Core.Validation;

/// <summary>
/// Last line of defence before publishing: scans every rendered artifact for
/// private identifiers, secrets, tokens and traces of private raw events.
/// </summary>
public sealed partial class PrivacyValidator : IOutputValidator
{
    public const string PrivateTermRuleId = "privacy.private-term";
    public const string TokenRuleId = "privacy.github-token";
    public const string SecretRuleId = "privacy.secret-value";
    public const string EmailRuleId = "privacy.email-address";
    public const string CommitHashRuleId = "privacy.commit-hash";
    public const string PrivateModelRuleId = "privacy.private-model-leak";
    public const string WebhookRuleId = "privacy.webhook-url";

    private static readonly string[] PrivateModelMarkers =
    [
        "PrivateActivityEvent",
        "RepositoryOpaqueId",
        "repositoryOpaqueId",
        "repository_opaque_id"
    ];

    public ValidationResult Validate(RenderedReport report, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<ValidationIssue>();

        foreach (var artifact in report.Artifacts)
        {
            issues.AddRange(ValidateArtifact(artifact, context));
        }

        return ValidationResult.FromIssues(issues);
    }

    /// <summary>Validates a raw text payload (used by the <c>validate</c> command on files).</summary>
    public ValidationResult ValidateContent(string content, string target, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var artifact = new RenderedArtifact
        {
            Name = target,
            RelativePath = target,
            Content = content ?? string.Empty,
            Kind = RenderedArtifactKind.PlainText
        };

        return ValidationResult.FromIssues(ValidateArtifact(artifact, context));
    }

    private static IEnumerable<ValidationIssue> ValidateArtifact(RenderedArtifact artifact, ValidationContext context)
    {
        var content = artifact.Content ?? string.Empty;
        var target = artifact.RelativePath;

        foreach (var term in context.ForbiddenTerms)
        {
            if (ContainsTerm(content, term))
            {
                yield return new ValidationIssue
                {
                    RuleId = PrivateTermRuleId,
                    Severity = ValidationSeverity.Error,
                    Message = $"Detected a private identifier ({SecretMasker.Mask(term)}) in the rendered output.",
                    Target = target
                };
            }
        }

        foreach (var secret in context.SecretValues)
        {
            if (secret.Length >= 8 && content.Contains(secret, StringComparison.Ordinal))
            {
                yield return new ValidationIssue
                {
                    RuleId = SecretRuleId,
                    Severity = ValidationSeverity.Error,
                    Message = "Detected a configured secret value in the rendered output.",
                    Target = target
                };
            }
        }

        if (context.DetectTokens)
        {
            if (GitHubTokenRegex().IsMatch(content))
            {
                yield return new ValidationIssue
                {
                    RuleId = TokenRuleId,
                    Severity = ValidationSeverity.Error,
                    Message = "Detected a value shaped like a GitHub token in the rendered output.",
                    Target = target
                };
            }

            if (SlackWebhookRegex().IsMatch(content))
            {
                yield return new ValidationIssue
                {
                    RuleId = WebhookRuleId,
                    Severity = ValidationSeverity.Error,
                    Message = "Detected a Slack webhook url in the rendered output.",
                    Target = target
                };
            }
        }

        foreach (var marker in PrivateModelMarkers)
        {
            if (content.Contains(marker, StringComparison.Ordinal))
            {
                yield return new ValidationIssue
                {
                    RuleId = PrivateModelRuleId,
                    Severity = ValidationSeverity.Error,
                    Message = "Detected a serialized private activity model in the rendered output.",
                    Target = target
                };
                break;
            }
        }

        if (context.DetectFullCommitHashes && FullCommitHashRegex().IsMatch(content))
        {
            yield return new ValidationIssue
            {
                RuleId = CommitHashRuleId,
                Severity = ValidationSeverity.Error,
                Message = "Detected a full git commit hash in the rendered output.",
                Target = target
            };
        }

        if (context.DetectEmailAddresses && EmailRegex().IsMatch(content))
        {
            yield return new ValidationIssue
            {
                RuleId = EmailRuleId,
                Severity = ValidationSeverity.Warning,
                Message = "Detected an email address in the rendered output.",
                Target = target
            };
        }
    }

    private static bool ContainsTerm(string content, string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 3)
        {
            return false;
        }

        var pattern = $"(?<![A-Za-z0-9]){Regex.Escape(term)}(?![A-Za-z0-9])";
        return Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
    }

    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{20,}", RegexOptions.None, 5000)]
    private static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"https://hooks\.slack\.com/services/\S+", RegexOptions.IgnoreCase, 5000)]
    private static partial Regex SlackWebhookRegex();

    [GeneratedRegex(@"(?<![0-9a-fA-F])[0-9a-fA-F]{40}(?![0-9a-fA-F])", RegexOptions.None, 5000)]
    private static partial Regex FullCommitHashRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.None, 5000)]
    private static partial Regex EmailRegex();
}
