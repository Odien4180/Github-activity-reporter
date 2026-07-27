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

    public async Task<IReadOnlyList<PublicRepositoryActivity>> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_timeout);

            var result = await _primary.SummarizeAsync(events, timeoutSource.Token).ConfigureAwait(false);
            if (result.Count > 0 || events.Count == 0)
            {
                return result;
            }

            _log.Warning("Primary summarizer returned no result, falling back to the rule based summarizer.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _log.Warning("Primary summarizer timed out, falling back to the rule based summarizer.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _log.Warning($"Primary summarizer failed ({exception.GetType().Name}), falling back to the rule based summarizer.");
        }

        return await _fallback.SummarizeAsync(events, cancellationToken).ConfigureAwait(false);
    }
}
