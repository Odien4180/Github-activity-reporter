using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.GitHub.Mapping;
using Octokit;

namespace GitHubActivityReporter.GitHub.Api;

/// <summary>Reads the authenticated user's activity feed through the GitHub REST API.</summary>
internal sealed class OctokitEventSource : IGitHubEventSource
{
    internal const string AuthenticatedUserEventsEndpoint = "user/events";

    private readonly IGitHubClient _client;
    private readonly IApiConnection _apiConnection;
    private readonly int _maxPages;
    private readonly IReporterLog _log;
    private readonly Dictionary<string, GitHubRepositoryInfo?> _repositoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PushCompareResult?> _pushCompareCache = new(StringComparer.Ordinal);
    public string? LastDiagnostics { get; private set; }

    public OctokitEventSource(
        IGitHubClient client,
        int maxPages = 3,
        IReporterLog? log = null,
        IApiConnection? apiConnection = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        // Tests can inject a stubbed API connection. Production uses the client's
        // configured connection so authentication and transport settings stay aligned.
        _apiConnection = apiConnection ?? new ApiConnection(client.Connection);
        _maxPages = Math.Clamp(maxPages, 1, 10);
        _log = log ?? NullReporterLog.Instance;
    }

    public async Task<IReadOnlyList<GitHubRawEvent>> GetUserEventsAsync(
        string userName,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        LastDiagnostics = null;

        var results = new List<GitHubRawEvent>();

        if (!await TryAppendAuthenticatedUserActivitiesAsync(results, since, cancellationToken).ConfigureAwait(false))
        {
            LastDiagnostics ??= "authenticated-user feed unavailable; used username-scoped fallback feed.";
            await AppendActivitiesAsync(
                    results,
                    since,
                    options => _client.Activity.Events.GetAllUserPerformed(userName, options),
                    cancellationToken,
                    "user-performed")
                .ConfigureAwait(false);
        }

        foreach (var organization in await GetCurrentOrganizationLoginsAsync(cancellationToken).ConfigureAwait(false))
        {
            await AppendActivitiesAsync(
                    results,
                    since,
                    options => _client.Activity.Events.GetAllForAnOrganization(userName, organization, options),
                    cancellationToken,
                    $"org:{organization}")
                .ConfigureAwait(false);
        }

        var privateCount = results.Count(e => e.IsPrivateRepository);
        var publicCount = results.Count - privateCount;
        _log.Debug($"Raw events fetched: {results.Count} total ({publicCount} public, {privateCount} private).");

        return results;
    }

    private async Task<bool> TryAppendAuthenticatedUserActivitiesAsync(
        List<GitHubRawEvent> results,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        try
        {
            // Prefer the authenticated-user feed because it can include private
            // repository activity for the token owner during workflow runs.
            await AppendActivitiesAsync(
                    results,
                    since,
                    options => _apiConnection.GetAll<Activity>(new Uri(AuthenticatedUserEventsEndpoint, UriKind.Relative), options),
                    cancellationToken,
                    "authenticated-user")
                .ConfigureAwait(false);

            return true;
        }
        catch (ApiException exception)
        {
            // Fall back to the username-scoped feed for older environments or
            // credentials that do not expose the authenticated-user endpoint.
            LastDiagnostics = BuildApiFailureDiagnostic("authenticated-user", exception);
            _log.Warning($"Authenticated-user feed failed: {LastDiagnostics}");
            return false;
        }
    }

    private async Task AppendActivitiesAsync(
        List<GitHubRawEvent> results,
        DateTimeOffset since,
        Func<ApiOptions, Task<IReadOnlyList<Activity>>> activityReader,
        CancellationToken cancellationToken,
        string feedLabel = "feed")
    {
        for (var page = 1; page <= _maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new ApiOptions { PageSize = 100, PageCount = 1, StartPage = page };
            var activities = await activityReader(options).ConfigureAwait(false);

            if (activities.Count == 0)
            {
                _log.Debug($"[{feedLabel}] page {page}: 0 events — stopping.");
                break;
            }

            var privateOnPage = activities.Count(a => !a.Public);
            _log.Debug($"[{feedLabel}] page {page}: {activities.Count} events ({privateOnPage} private).");

            foreach (var activity in activities)
            {
                PushCompareResult? compareResult = null;
                if (activity.CreatedAt >= since
                    && activity.Type == "PushEvent"
                    && activity.Payload is PushEventPayload pushPayload)
                {
                    compareResult = await TryGetCompareResultAsync(
                            activity.Repo?.Name,
                            pushPayload.Before,
                            pushPayload.Head,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                results.AddRange(OctokitActivityMapper.Map(activity, compareResult));
            }

            // The feed is ordered newest first, so we can stop as soon as we passed the window.
            if (activities[^1].CreatedAt < since)
            {
                break;
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetCurrentOrganizationLoginsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var logins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var page = 1; page <= _maxPages; page++)
            {
                var organizations = await _client.Organization
                    .GetAllForCurrent(new ApiOptions { PageSize = 100, PageCount = 1, StartPage = page })
                    .ConfigureAwait(false);

                if (organizations.Count == 0)
                {
                    break;
                }

                foreach (var login in organizations
                             .Select(organization => organization.Login)
                             .Where(login => !string.IsNullOrWhiteSpace(login)))
                {
                    logins.Add(login);
                }

                if (organizations.Count < 100)
                {
                    break;
                }
            }

            return logins.ToArray();
        }
        catch (ApiException)
        {
            _log.Warning("GitHub organization discovery failed; private organization activity may be incomplete for this run.");
            return Array.Empty<string>();
        }
    }

    internal async Task<PushCompareResult?> TryGetCompareResultAsync(
        string? repositoryFullName,
        string? before,
        string? head,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(repositoryFullName)
            || string.IsNullOrWhiteSpace(before)
            || string.IsNullOrWhiteSpace(head))
        {
            return null;
        }

        var separator = repositoryFullName.IndexOf('/');
        if (separator <= 0 || separator == repositoryFullName.Length - 1)
        {
            return null;
        }

        var cacheKey = $"{repositoryFullName}\n{before}\n{head}";
        if (_pushCompareCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var owner = repositoryFullName[..separator];
        var name = repositoryFullName[(separator + 1)..];

        try
        {
            var comparison = await _client.Repository.Commit
                .Compare(owner, name, before, head)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            int? count = comparison.TotalCommits > 0 ? comparison.TotalCommits : null;
            var subjects = comparison.Commits?
                .Select(commit => FirstLine(commit.Commit?.Message))
                .Where(subject => !string.IsNullOrWhiteSpace(subject))
                .Cast<string>()
                .ToArray() ?? Array.Empty<string>();
            var files = comparison.Files?
                .Where(f => !string.IsNullOrWhiteSpace(f.Filename))
                .Select(f => f.Filename)
                .ToArray() ?? Array.Empty<string>();
            int? additions = comparison.Files?.Sum(f => f.Additions) is int a and > 0 ? a : null;
            int? deletions = comparison.Files?.Sum(f => f.Deletions) is int d and > 0 ? d : null;

            var result = new PushCompareResult
            {
                CommitCount = count,
                CommitSubjects = subjects,
                ChangedPaths = files,
                Additions = additions,
                Deletions = deletions,
                ChangedFiles = files.Length > 0 ? files.Length : null
            };

            _pushCompareCache[cacheKey] = result;
            return result;
        }
        catch (NotFoundException)
        {
            _pushCompareCache[cacheKey] = null;
            return null;
        }
        catch (ApiException)
        {
            _pushCompareCache[cacheKey] = null;
            return null;
        }

    }

    private static string BuildApiFailureDiagnostic(string feedLabel, ApiException exception)
    {
        var status = ((int)exception.StatusCode).ToString();
        var apiMessage = string.IsNullOrWhiteSpace(exception.ApiError?.Message)
            ? exception.Message
            : exception.ApiError.Message;

        return $"{feedLabel} status={status}, error={apiMessage}";
    }

    private static string? FirstLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var index = message.IndexOfAny(['\r', '\n']);
        return (index < 0 ? message : message[..index]).Trim();
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
