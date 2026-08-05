using System.Net;
using System.Text;
using System.Text.Json;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Summarization.AI;
using GitHubActivityReporter.Summarization.Fallback;
using GitHubActivityReporter.Summarization.RuleBased;
using NSubstitute;

namespace GitHubActivityReporter.Core.Tests;

public sealed class AiSummarizerTests
{
    [Fact]
    public async Task Ai_summarizer_uses_public_facts_and_preserves_deterministic_metrics()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("공개 기능을 개선했습니다."));
        var settings = new SummarySettings { Language = "ko" };
        var privacy = new PublicPrivacySettings();
        var summarizer = new AiPublicActivitySummarizer(client, settings, privacy);

        var result = await summarizer.SummarizeAsync(PublicEvents(), CancellationToken.None);

        var activity = Assert.Single(result.Repositories);
        Assert.Equal(2, activity.Metrics.TotalCount);
        Assert.Equal("공개 기능을 개선했습니다.", activity.Summary);
        Assert.Equal("개발 흐름의 안정성을 개선했습니다.", result.Narrative.Headline);
        Assert.NotEmpty(result.Narrative.Highlights);
        await client.Received(1).GenerateAsync(
            Arg.Is<AiTextRequest>(request =>
                request != null
                && request.Input.Contains("example/public", StringComparison.Ordinal)
                && request.MaxOutputTokens == settings.Ai.MaxOutputTokens),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ai_prompt_omits_titles_when_public_title_exposure_is_disabled()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("Summary"));
        var privacy = new PublicPrivacySettings
        {
            ExposePullRequestTitles = false,
            ExposeIssueTitles = false,
            ExposeReleaseNames = false,
            ExposeCommitMessages = false
        };

        await new AiPublicActivitySummarizer(client, new SummarySettings(), privacy)
            .SummarizeAsync(PublicEvents(), CancellationToken.None);

        await client.Received(1).GenerateAsync(
            Arg.Is<AiTextRequest>(request => request != null && !request.Input.Contains("PUBLIC_TITLE_SENTINEL", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ai_prompt_includes_allowed_public_repository_context_and_requests_descriptive_prose()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("Improved the report generation workflow."));
        var privacy = new PublicPrivacySettings
        {
            ExposeRepositoryDescriptions = true,
            ExposeLanguages = true,
            ExposeTopics = true
        };

        await new AiPublicActivitySummarizer(client, new SummarySettings { Language = "en" }, privacy)
            .SummarizeAsync(PublicEvents(), CancellationToken.None);

        await client.Received(1).GenerateAsync(
            Arg.Is<AiTextRequest>(request =>
                request != null
                && request.Input.Contains("Generates readable GitHub activity reports", StringComparison.Ordinal)
                && request.Input.Contains("C#", StringComparison.Ordinal)
                && request.Input.Contains("reporting", StringComparison.Ordinal)
                && request.Instructions.Contains("Treat counts as supporting detail", StringComparison.Ordinal)
                && request.Instructions.Contains("what the work was about", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Public_commit_messages_are_ai_only_evidence_when_explicitly_enabled()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("Improved retry reliability."));
        var settings = new SummarySettings { Language = "en" };
        settings.Ai.IncludePublicCommitMessages = true;
        var privacy = new PublicPrivacySettings { ExposeCommitMessages = false };

        await new AiPublicActivitySummarizer(client, settings, privacy)
            .SummarizeAsync(PublicEvents(), CancellationToken.None);

        await client.Received(1).GenerateAsync(
            Arg.Is<AiTextRequest>(request => request != null && request.Input.Contains("Implement reliable retry flow", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Verbatim_commit_message_in_ai_output_is_rejected_and_falls_back()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("Implement reliable retry flow"));
        var settings = new SummarySettings { Language = "en" };
        settings.Ai.IncludePublicCommitMessages = true;
        var primary = new AiPublicActivitySummarizer(client, settings, new PublicPrivacySettings());
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(settings),
            timeout: TimeSpan.FromSeconds(1));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.DoesNotContain(
            "Implement reliable retry flow",
            result.Narrative.Headline ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Narrative.Highlights,
            highlight => highlight.Contains("Implement reliable retry flow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Invalid_ai_response_falls_back_to_rule_based_summary()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>()).Returns("not-json");
        var settings = new SummarySettings { Language = "en" };
        var primary = new AiPublicActivitySummarizer(client, settings, new PublicPrivacySettings());
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(settings),
            timeout: TimeSpan.FromSeconds(1));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.Single(result.Repositories);
        Assert.False(string.IsNullOrWhiteSpace(result.Repositories[0].Summary));
        Assert.NotEqual("not-json", result.Repositories[0].Summary);
        Assert.False(string.IsNullOrWhiteSpace(result.Narrative.Headline));
    }

    [Fact]
    public async Task Rule_based_summary_uses_commit_count_fallback_for_commit_only_activity()
    {
        var events = PublicEvents().Where(item => item.Type == ActivityType.Commit).ToArray();

        var result = await new RuleBasedPublicActivitySummarizer(new SummarySettings { Language = "ko" })
            .SummarizeAsync(events, CancellationToken.None);

        var repository = Assert.Single(result.Repositories);
        Assert.Contains("커밋", repository.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Generates readable GitHub activity reports", repository.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("구현과 정비", repository.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rule_based_summary_prefers_changed_paths_when_enabled()
    {
        var settings = new SummarySettings { Language = "ko", UsePublicChangeDetails = true, PublicChangeDetailLevel = "standard" };

        var result = await new RuleBasedPublicActivitySummarizer(settings)
            .SummarizeAsync(PublicEvents(), CancellationToken.None);

        var repository = Assert.Single(result.Repositories);
        Assert.Contains("설정 흐름", repository.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAi_client_uses_responses_api_limits_and_extracts_output_text()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"{\"summaries\":[]}"}]}]}"""));
        var client = new OpenAiResponsesClient("api-key", "gpt-5.6-sol", new HttpClient(handler), maxRetries: 0);

        var result = await client.GenerateAsync(
            new AiTextRequest { Instructions = "instructions", Input = "input", MaxOutputTokens = 321 },
            CancellationToken.None);

        Assert.Equal("{\"summaries\":[]}", result);
        Assert.Equal("https://api.openai.com/v1/responses", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("api-key", handler.AuthorizationParameter);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("gpt-5.6-sol", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(321, body.RootElement.GetProperty("max_output_tokens").GetInt32());
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
    }

    [Fact]
    public async Task GitHub_models_client_uses_official_inference_endpoint()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """{"choices":[{"message":{"role":"assistant","content":"{\"summaries\":[]}"}}]}"""));
        var client = new GitHubModelsClient("github-token", "openai/gpt-4.1", new HttpClient(handler), maxRetries: 0);

        var result = await client.GenerateAsync(
            new AiTextRequest { Instructions = "instructions", Input = "input", MaxOutputTokens = 222 },
            CancellationToken.None);

        Assert.Equal("{\"summaries\":[]}", result);
        Assert.Equal("https://models.github.ai/inference/chat/completions", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("openai/gpt-4.1", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(222, body.RootElement.GetProperty("max_tokens").GetInt32());
    }

    private static IReadOnlyList<PublicActivityEvent> PublicEvents() =>
    [
        new PublicActivityEvent
        {
            Type = ActivityType.PullRequestMerged,
            RepositoryName = "example/public",
            RepositoryUrl = "https://github.com/example/public",
            Description = "Generates readable GitHub activity reports",
            Language = "C#",
            Topics = ["reporting", "github"],
            Title = "PUBLIC_TITLE_SENTINEL",
            ChangedPaths = ["src/Config/Flow.cs", "src/Validation/ConnectionGuard.cs"],
            Additions = 20,
            Deletions = 5,
            ChangedFiles = 2,
            OccurredAt = DateTimeOffset.Parse("2026-07-27T00:00:00Z")
        },
        new PublicActivityEvent
        {
            Type = ActivityType.Commit,
            RepositoryName = "example/public",
            RepositoryUrl = "https://github.com/example/public",
            Description = "Generates readable GitHub activity reports",
            Language = "C#",
            Topics = ["reporting", "github"],
            Title = "Implement reliable retry flow",
            ChangedPaths = ["src/Validation/RetryPolicy.cs"],
            Additions = 12,
            Deletions = 3,
            ChangedFiles = 1,
            OccurredAt = DateTimeOffset.Parse("2026-07-26T00:00:00Z")
        }
    ];

    private static string ValidResponse(string repositorySummary)
        => JsonSerializer.Serialize(new
        {
            headline = "개발 흐름의 안정성을 개선했습니다.",
            highlights = new[] { "재시도 흐름과 변경 전달 과정을 정비했습니다." },
            summaries = new[] { new { id = "r1", summary = repositorySummary } }
        });

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ApiVersion = request.Headers.TryGetValues("X-GitHub-Api-Version", out var values)
                ? values.Single()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
