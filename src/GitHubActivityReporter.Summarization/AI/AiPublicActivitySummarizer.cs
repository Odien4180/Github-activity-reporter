using System.Text.Json;
using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Summarization.RuleBased;

namespace GitHubActivityReporter.Summarization.AI;

/// <summary>
/// Adds AI-written summaries to deterministic public repository activities.
/// The API surface only accepts PublicActivityEvent, so private events cannot enter the prompt.
/// </summary>
public sealed class AiPublicActivitySummarizer : IPublicActivitySummarizer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly IAiTextClient _client;
    private readonly SummarySettings _summary;
    private readonly PublicPrivacySettings _privacy;
    private readonly RuleBasedPublicActivitySummarizer _baseline;

    public AiPublicActivitySummarizer(
        IAiTextClient client,
        SummarySettings summary,
        PublicPrivacySettings privacy)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        _privacy = privacy ?? throw new ArgumentNullException(nameof(privacy));
        _baseline = new RuleBasedPublicActivitySummarizer(summary);
    }

    public async Task<PublicActivitySummary> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        var baseline = await _baseline.SummarizeAsync(events, cancellationToken).ConfigureAwait(false);
        if (baseline.Repositories.Count == 0)
        {
            return baseline;
        }

        var prompt = BuildPrompt(events, baseline.Repositories);
        var response = await _client.GenerateAsync(
            new AiTextRequest
            {
                Instructions = BuildInstructions(),
                Input = prompt.Input,
                MaxOutputTokens = _summary.Ai.MaxOutputTokens
            },
            cancellationToken).ConfigureAwait(false);

        var parsed = ParseResponse(response, baseline.Repositories.Count, prompt.RawCommitMessages);
        var repositories = baseline.Repositories
            .Select((activity, index) => parsed.RepositorySummaries.TryGetValue($"r{index + 1}", out var summary)
                ? activity with { Summary = summary }
                : activity)
            .ToArray();

        return new PublicActivitySummary
        {
            Repositories = repositories,
            Narrative = parsed.Narrative
        };
    }

    private PromptBuildResult BuildPrompt(
        IReadOnlyList<PublicActivityEvent> events,
        IReadOnlyList<PublicRepositoryActivity> activities)
    {
        var allowedRepositories = activities
            .Select((activity, index) => new { activity.RepositoryName, Id = $"r{index + 1}" })
            .ToDictionary(item => item.RepositoryName, item => item.Id, StringComparer.OrdinalIgnoreCase);

        var selected = events
            .Where(e => allowedRepositories.ContainsKey(e.RepositoryName))
            .OrderByDescending(e => e.OccurredAt)
            .Take(_summary.Ai.MaxInputEvents)
            .Select(e => new PromptEvent
            {
                Id = allowedRepositories[e.RepositoryName],
                Repository = _privacy.ExposeRepositoryNames ? Truncate(e.RepositoryName, 200) : null,
                Type = e.Type.ToString(),
                Title = CanExposeTitle(e) ? Truncate(e.Title, 300) : null,
                ChangedPaths = _summary.UsePublicChangeDetails
                    ? e.ChangedPaths.Take(_summary.PublicChangeDetailLevel == "detailed" ? 10 : 5).Select(path => Truncate(path, 200)).Where(path => path is not null).Cast<string>().ToArray()
                    : Array.Empty<string>(),
                Additions = _summary.UsePublicChangeDetails ? e.Additions : null,
                Deletions = _summary.UsePublicChangeDetails ? e.Deletions : null,
                ChangedFiles = _summary.UsePublicChangeDetails ? e.ChangedFiles : null,
                OccurredAt = e.OccurredAt
            })
            .ToList();

        string Serialize() => JsonSerializer.Serialize(new
        {
            language = _summary.Language,
            style = _summary.Style,
            repositories = activities.Select((activity, index) => new
            {
                id = $"r{index + 1}",
                name = _privacy.ExposeRepositoryNames ? activity.RepositoryName : null,
                description = _privacy.ExposeRepositoryDescriptions ? Truncate(activity.Description, 500) : null,
                language = _privacy.ExposeLanguages ? Truncate(activity.Language, 100) : null,
                topics = _privacy.ExposeTopics
                    ? activity.Topics.Take(10).Select(topic => Truncate(topic, 100)).Where(topic => topic is not null)
                    : null,
                metrics = activity.Metrics
            }),
            events = selected
        }, JsonOptions);

        var prompt = Serialize();
        while (prompt.Length > _summary.Ai.MaxInputCharacters && selected.Count > 0)
        {
            selected.RemoveAt(selected.Count - 1);
            prompt = Serialize();
        }

        if (prompt.Length > _summary.Ai.MaxInputCharacters)
        {
            throw new InvalidOperationException("AI summary metadata exceeds summary.ai.max_input_characters.");
        }

        var rawCommitMessages = selected
            .Where(e => e.Type == nameof(ActivityType.Commit) && !string.IsNullOrWhiteSpace(e.Title))
            .Select(e => e.Title!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PromptBuildResult { Input = prompt, RawCommitMessages = rawCommitMessages };
    }

    private string BuildInstructions()
        => $"""
           Summarize only the public GitHub activity facts in the supplied JSON.
           Produce a useful period-level narrative and exactly one concise summary per repository id.
           Describe what changed or what the work was about. Use repository descriptions, languages, topics,
           event titles, changed paths, and diff statistics as context, and connect related facts into a concrete account of the work.
           Treat counts as supporting detail, not as the main summary. When any qualitative evidence is
           available, do not write a summary that merely lists counts or says that N activities occurred.
           The headline should state the dominant development focus. Highlights should group related work
           into 3 to 5 outcome-oriented bullets when enough evidence exists; use fewer when activity is sparse.
           Use commit subjects as evidence for themes, but paraphrase them and never quote or reproduce a
           commit subject verbatim. Prefer concrete themes such as delivery, reliability, testing,
           documentation, automation, or maintenance only when supported by the supplied facts.
           Do not invent work, names, links, organizations, metrics, or conclusions.
           Do not mention information that is absent from the input.
           Write in {(_summary.Language == "ko" ? "Korean" : "English")} using a {_summary.Style} style.
           Keep the headline under 200 characters and every highlight and repository summary under 300 characters.
           Keep every string on one line. Avoid generic praise, activity-log phrasing, and repeated metric-only sentences.
           """;

    private bool CanExposeTitle(PublicActivityEvent activity)
        => activity.Type switch
        {
            ActivityType.PullRequestOpened or ActivityType.PullRequestMerged or ActivityType.PullRequestClosed
                => _privacy.ExposePullRequestTitles,
            ActivityType.IssueOpened or ActivityType.IssueClosed => _privacy.ExposeIssueTitles,
            ActivityType.ReleasePublished => _privacy.ExposeReleaseNames,
            ActivityType.Commit => _summary.Ai.IncludePublicCommitMessages,
            _ => false
        } && !string.IsNullOrWhiteSpace(activity.Title);

    private static ParsedResponse ParseResponse(
        string response,
        int repositoryCount,
        IReadOnlyList<string> rawCommitMessages)
    {
        using var json = JsonDocument.Parse(response);
        var root = json.RootElement;

        // Some models wrap the entire response under a "result" envelope.
        // Try root first, then fall back to root["result"] for headline and highlights.
        var narrativeRoot = root;
        if (ReadSingleLine(root, "headline", 200) is null
            && root.TryGetProperty("result", out var resultEnvelope)
            && resultEnvelope.ValueKind == JsonValueKind.Object
            && ReadSingleLine(resultEnvelope, "headline", 200) is not null)
        {
            narrativeRoot = resultEnvelope;
        }

        var headline = ReadSingleLine(narrativeRoot, "headline", 200)
                       ?? throw new JsonException("AI summary response is missing a valid headline.");
        if (!narrativeRoot.TryGetProperty("highlights", out var highlightsElement)
            || highlightsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("AI summary response is missing the highlights array.");
        }

        var highlights = highlightsElement
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? CleanSingleLine(item.GetString()) : null)
            .Where(item => !string.IsNullOrWhiteSpace(item) && item.Length <= 300)
            .Cast<string>()
            .Take(5)
            .ToArray();
        if (highlights.Length == 0)
        {
            throw new JsonException("AI summary response contains no usable highlights.");
        }

        if (!TryGetSummariesElement(root, out var summaries))
        {
            throw new JsonException("AI summary response is missing the summaries array.");
        }

        var accepted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in summaries.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement)
                || !item.TryGetProperty("summary", out var summaryElement))
            {
                continue;
            }

            var id = idElement.GetString();
            var summary = CleanSingleLine(summaryElement.GetString());
            if (id is null
                || !TryParseRepositoryId(id, repositoryCount)
                || string.IsNullOrWhiteSpace(summary)
                || summary.Length > 300)
            {
                continue;
            }

            accepted[id] = summary;
        }

        if (accepted.Count != repositoryCount)
        {
            throw new JsonException("AI summary response must contain one usable summary per repository.");
        }

        var allOutput = new[] { headline }.Concat(highlights).Concat(accepted.Values).ToArray();
        foreach (var rawMessage in rawCommitMessages.Where(message => message.Trim().Length >= 12))
        {
            if (allOutput.Any(output => output.Contains(rawMessage.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new JsonException("AI summary reproduced a raw commit message.");
            }
        }

        return new ParsedResponse
        {
            Narrative = new PublicActivityNarrative { Headline = headline, Highlights = highlights },
            RepositorySummaries = accepted
        };
    }

    private static string? ReadSingleLine(JsonElement root, string propertyName, int maxLength)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = CleanSingleLine(element.GetString());
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength ? value : null;
    }

    private static string? CleanSingleLine(string? value)
        => value?.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static bool TryParseRepositoryId(string id, int repositoryCount)
        => id.Length > 1
           && id[0] == 'r'
           && int.TryParse(id.AsSpan(1), out var value)
           && value >= 1
           && value <= repositoryCount;

    private static bool TryGetSummariesElement(JsonElement root, out JsonElement summaries)
    {
        if (root.TryGetProperty("summaries", out summaries)
            && summaries.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (root.TryGetProperty("repository_summaries", out summaries)
            && summaries.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (root.TryGetProperty("repositories", out summaries)
            && summaries.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (root.TryGetProperty("result", out var result)
            && result.ValueKind == JsonValueKind.Object)
        {
            if (result.TryGetProperty("summaries", out summaries)
                && summaries.ValueKind == JsonValueKind.Array)
            {
                return true;
            }

            if (result.TryGetProperty("repository_summaries", out summaries)
                && summaries.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }

        summaries = default;
        return false;
    }

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];

    private sealed record PromptEvent
    {
        public required string Id { get; init; }
        public string? Repository { get; init; }
        public required string Type { get; init; }
        public string? Title { get; init; }
        public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();
        public int? Additions { get; init; }
        public int? Deletions { get; init; }
        public int? ChangedFiles { get; init; }
        public required DateTimeOffset OccurredAt { get; init; }
    }

    private sealed record PromptBuildResult
    {
        public required string Input { get; init; }
        public required IReadOnlyList<string> RawCommitMessages { get; init; }
    }

    private sealed record ParsedResponse
    {
        public required PublicActivityNarrative Narrative { get; init; }
        public required IReadOnlyDictionary<string, string> RepositorySummaries { get; init; }
    }
}
