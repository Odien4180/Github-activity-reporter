using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Summarization.Fallback;

/// <summary>
/// Runs a primary summarizer (for example an AI based one) and transparently falls
/// back to the deterministic summarizer when it fails, times out or returns nothing.
/// </summary>
public sealed class FallbackPublicActivitySummarizer : IPublicActivitySummarizer
{
    private readonly IPublicActivitySummarizer _primary;
    private readonly IPublicActivitySummarizer _fallback;
    private readonly IReporterLog _log;
    private readonly TimeSpan _timeout;

    public FallbackPublicActivitySummarizer(
        IPublicActivitySummarizer primary,
        IPublicActivitySummarizer fallback,
        IReporterLog? log = null,
        TimeSpan? timeout = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _log = log ?? NullReporterLog.Instance;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<PublicActivitySummary> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        string? fallbackReason = null;

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_timeout);

            var result = await _primary.SummarizeAsync(events, timeoutSource.Token).ConfigureAwait(false);
            if (result.Repositories.Count > 0 || events.Count == 0)
            {
                return result;
            }

            fallbackReason = "primary-empty";
            _log.Warning("Primary summarizer returned no result, falling back to the rule based summarizer.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            fallbackReason = $"timeout({(int)_timeout.TotalSeconds}s)";
            _log.Warning($"Primary summarizer timed out after {(int)_timeout.TotalSeconds}s, falling back to the rule based summarizer.");
        }
        catch (HttpRequestException httpEx)
        {
            var statusPart = httpEx.StatusCode.HasValue
                ? $"HTTP {(int)httpEx.StatusCode.Value}"
                : "network error";
            fallbackReason = $"http-error({statusPart})";

            var innerPart = httpEx.InnerException is { } inner
                ? $" InnerException: [{inner.GetType().Name}] {SanitizeMessage(inner.Message)}"
                : string.Empty;

            _log.Warning(
                $"Primary summarizer failed ({httpEx.GetType().Name}): {statusPart} — {SanitizeMessage(httpEx.Message)}{innerPart}. " +
                $"Falling back to the rule based summarizer.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            fallbackReason = $"error({exception.GetType().Name})";

            var innerPart = exception.InnerException is { } inner
                ? $" InnerException: [{inner.GetType().Name}] {SanitizeMessage(inner.Message)}"
                : string.Empty;

            _log.Warning(
                $"Primary summarizer failed ({exception.GetType().Name}): {SanitizeMessage(exception.Message)}{innerPart}. " +
                $"Falling back to the rule based summarizer.");
        }

        var fallbackResult = await _fallback.SummarizeAsync(events, cancellationToken).ConfigureAwait(false);
        return fallbackResult with { FallbackUsed = true, FallbackReason = fallbackReason };
    }

    /// <summary>
    /// Returns a safe version of an exception message with tokens/secrets redacted.
    /// Strips anything resembling a bearer token, API key, or long hex/base64 sequence.
    /// </summary>
    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // Redact bearer tokens
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"(?i)(bearer\s+)[A-Za-z0-9\-._~+/]+=*",
            "$1[REDACTED]");

        // Redact long hex/base64/token-like sequences (32+ chars)
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[A-Za-z0-9+/\-_]{32,}={0,2}",
            "[REDACTED]");

        return sanitized;
    }
}
