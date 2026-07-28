using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Pipelines;
using GitHubActivityReporter.Core.Security;
using GitHubActivityReporter.GitHub.Api;
using Octokit;

namespace GitHubActivityReporter.GitHub.Collectors;

/// <summary>
/// Collects a user's GitHub activity and classifies it by repository visibility.
/// Private activity is turned into opaque events immediately; only public activity
/// is enriched with repository metadata.
/// </summary>
public sealed class GitHubActivityCollector : IActivityCollector
{
    public const string UserAgent = "github-activity-reporter";

    private readonly IGitHubEventSource _source;
    private readonly IPrivateTermRegistry _privateTerms;
    private readonly IReporterLog _log;

    internal GitHubActivityCollector(
        IGitHubEventSource source,
        IPrivateTermRegistry privateTerms,
        IReporterLog? log = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _privateTerms = privateTerms ?? throw new ArgumentNullException(nameof(privateTerms));
        _log = log ?? NullReporterLog.Instance;
    }

    /// <summary>Creates a collector backed by the GitHub REST API.</summary>
    public static GitHubActivityCollector Create(
        string token,
        IPrivateTermRegistry privateTerms,
        IReporterLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(privateTerms);

        var client = new GitHubClient(new ProductHeaderValue(UserAgent))
        {
            Credentials = new Credentials(token)
        };

        return new GitHubActivityCollector(new OctokitEventSource(client, log: log), privateTerms, log);
    }

    public async Task<CollectedActivity> CollectAsync(
        CollectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawEvents = await _source
            .GetUserEventsAsync(request.UserName, request.PeriodStart, cancellationToken)
            .ConfigureAwait(false);

        var deduplicated = rawEvents
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();

        var builder = new CollectedActivityBuilder(_privateTerms);
        var metadataCache = new Dictionary<string, GitHubRepositoryInfo?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawEvent in deduplicated)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GitHubRepositoryInfo? metadata = null;
            if (!rawEvent.IsPrivateRepository && request.CollectPublic && request.Contains(rawEvent.OccurredAt))
            {
                if (!metadataCache.TryGetValue(rawEvent.RepositoryFullName, out metadata))
                {
                    metadata = await _source
                        .GetPublicRepositoryAsync(rawEvent.RepositoryFullName, cancellationToken)
                        .ConfigureAwait(false);
                    metadataCache[rawEvent.RepositoryFullName] = metadata;
                }
            }

            builder.Add(
                new ActivityInput
                {
                    Type = rawEvent.Type,
                    RepositoryFullName = rawEvent.RepositoryFullName,
                    IsPrivateRepository = rawEvent.IsPrivateRepository,
                    OccurredAt = rawEvent.OccurredAt,
                    RepositoryUrl = metadata?.HtmlUrl ?? (rawEvent.IsPrivateRepository ? null : $"https://github.com/{rawEvent.RepositoryFullName}"),
                    RepositoryDescription = metadata?.Description,
                    Language = metadata?.Language,
                    Topics = metadata?.Topics ?? Array.Empty<string>(),
                    Title = rawEvent.Title,
                    Url = rawEvent.Url
                },
                request);
        }

        var collected = builder.Build();

        _log.Info($"Collected {collected.PublicEvents.Count} public events.");
        _log.Info(collected.PrivateEventCount > 0
            ? "Collected private activity metrics."
            : "No private activity in this period.");

        return collected;
    }
}
