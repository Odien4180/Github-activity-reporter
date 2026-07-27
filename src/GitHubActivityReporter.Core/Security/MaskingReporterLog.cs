using System.Text.RegularExpressions;
using GitHubActivityReporter.Core.Abstractions;

namespace GitHubActivityReporter.Core.Security;

/// <summary>
/// Decorator that removes tokens, secrets and known private identifiers from every
/// log line before it reaches the terminal or a workflow log.
/// </summary>
public sealed partial class MaskingReporterLog : IReporterLog
{
    private readonly IReporterLog _inner;
    private readonly IPrivateTermRegistry? _privateTerms;
    private readonly IReadOnlyList<string> _secrets;

    public MaskingReporterLog(
        IReporterLog inner,
        IPrivateTermRegistry? privateTerms = null,
        IEnumerable<string>? secrets = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _privateTerms = privateTerms;
        _secrets = (secrets ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    public void Debug(string message) => _inner.Debug(Sanitize(message));

    public void Info(string message) => _inner.Info(Sanitize(message));

    public void Warning(string message) => _inner.Warning(Sanitize(message));

    public void Error(string message) => _inner.Error(Sanitize(message));

    public string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var sanitized = SecretMasker.MaskAll(message, _secrets);

        if (_privateTerms is { Count: > 0 })
        {
            sanitized = SecretMasker.MaskAll(sanitized, _privateTerms.Terms);
        }

        sanitized = TokenRegex().Replace(sanitized, SecretMasker.FullMask);
        return sanitized;
    }

    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{20,}", RegexOptions.None, 2000)]
    private static partial Regex TokenRegex();
}
