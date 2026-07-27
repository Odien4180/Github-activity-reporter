using GitHubActivityReporter.GitHub.Mapping;
using Octokit;

namespace GitHubActivityReporter.GitHub.Api;

/// <summary>Reads the authenticated user's activity feed through the GitHub REST API.</summary>
internal sealed class OctokitEventSource : IGitHubEventSource
{
    private readonly IGitHubClient _client;
    private readonly int _maxPages;
    private readonly Dictionary<string, GitHubRepositoryInfo?> _repositoryCache = new(StringComparer.OrdinalIgnoreCase);

    public OctokitEventSource(IGitHubClient client, int maxPages = 3)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _maxPages = Math.Clamp(maxPages, 1, 10);
    }

    public async Task<IReadOnlyList<GitHubRawEvent>> GetUserEventsAsync(
        string userName,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var results = new List<GitHubRawEvent>();

        for (var page = 1; page <= _maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new ApiOptions { PageSize = 100, PageCount = 1, StartPage = page };
            var activities = await _client.Activity.Events
                .GetAllUserPerformed(userName, options)
                .ConfigureAwait(false);

            if (activities.Count == 0)
            {
                break;
            }

            foreach (var activity in activities)
            {
                results.AddRange(OctokitActivityMapper.Map(activity));
            }

            // The feed is ordered newest first, so we can stop as soon as we passed the window.
            if (activities[^1].CreatedAt < since)
            {
                break;
            }
        }

        return results;
    }

    public async Task<GitHubRepositoryInfo?> GetPublicRepositoryAsync(
        string fullName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        cancellationToken.ThrowIfCancellationRequested();

        if (_repositoryCache.TryGetValue(fullName, out var cached))
        {
            return cached;
        }

        var separator = fullName.IndexOf('/');
        if (separator <= 0)
        {
            _repositoryCache[fullName] = null;
            return null;
        }

        var owner = fullName[..separator];
        var name = fullName[(separator + 1)..];

        try
        {
            var repository = await _client.Repository.Get(owner, name).ConfigureAwait(false);

            // Defensive: never enrich anything that is not public.
            if (repository.Private)
            {
                _repositoryCache[fullName] = null;
                return null;
            }

            var info = new GitHubRepositoryInfo
            {
                FullName = repository.FullName,
                HtmlUrl = repository.HtmlUrl,
                Description = repository.Description,
                Language = repository.Language,
                Topics = repository.Topics?.ToArray() ?? Array.Empty<string>()
            };

            _repositoryCache[fullName] = info;
            return info;
        }
        catch (NotFoundException)
        {
            _repositoryCache[fullName] = null;
            return null;
        }
        catch (ApiException)
        {
            _repositoryCache[fullName] = null;
            return null;
        }
    }
}
