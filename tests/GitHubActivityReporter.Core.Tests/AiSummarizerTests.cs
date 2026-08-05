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
    public async Task Ai_summarizer_accepts_repository_summaries_alias()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                headline = "개선 작업을 진행했습니다.",
                highlights = new[] { "핵심 흐름을 정리했습니다." },
                repository_summaries = new[] { new { id = "r1", summary = "저장소 요약입니다." } }
            }));
        var summarizer = new AiPublicActivitySummarizer(client, new SummarySettings(), new PublicPrivacySettings());

        var result = await summarizer.SummarizeAsync(PublicEvents(), CancellationToken.None);

        var repo = Assert.Single(result.Repositories);
        Assert.Equal("저장소 요약입니다.", repo.Summary);
    }

    [Fact]
    public async Task Ai_summarizer_accepts_nested_result_summaries()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                headline = "개선 작업을 진행했습니다.",
                highlights = new[] { "핵심 흐름을 정리했습니다." },
                result = new
                {
                    summaries = new[] { new { id = "r1", summary = "중첩 요약입니다." } }
                }
            }));
        var summarizer = new AiPublicActivitySummarizer(client, new SummarySettings(), new PublicPrivacySettings());

        var result = await summarizer.SummarizeAsync(PublicEvents(), CancellationToken.None);

        var repo = Assert.Single(result.Repositories);
        Assert.Equal("중첩 요약입니다.", repo.Summary);
    }

    [Fact]
    public async Task Ai_summarizer_accepts_fully_nested_result_envelope()
    {
        // Some Copilot models return headline, highlights, and summaries all under a "result" key.
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new
            {
                result = new
                {
                    headline = "전체 중첩 응답입니다.",
                    highlights = new[] { "중첩된 하이라이트입니다." },
                    summaries = new[] { new { id = "r1", summary = "중첩 저장소 요약입니다." } }
                }
            }));
        var summarizer = new AiPublicActivitySummarizer(client, new SummarySettings(), new PublicPrivacySettings());

        var result = await summarizer.SummarizeAsync(PublicEvents(), CancellationToken.None);

        var repo = Assert.Single(result.Repositories);
        Assert.Equal("중첩 저장소 요약입니다.", repo.Summary);
        Assert.Equal("전체 중첩 응답입니다.", result.Narrative.Headline);
        Assert.NotEmpty(result.Narrative.Highlights);
    }

    [Fact]
    public async Task Rule_based_summary_describes_changed_work_for_commit_only_activity()
    {
        var events = PublicEvents().Where(item => item.Type == ActivityType.Commit).ToArray();

        var result = await new RuleBasedPublicActivitySummarizer(new SummarySettings
            {
                Language = "ko",
                UsePublicChangeDetails = true
            })
            .SummarizeAsync(events, CancellationToken.None);

        var repository = Assert.Single(result.Repositories);
        Assert.Contains("안정성", repository.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("커밋 1건", repository.Summary, StringComparison.Ordinal);
        Assert.Contains(result.Narrative.Highlights, highlight =>
            highlight.Contains("안정성", StringComparison.Ordinal));
        Assert.DoesNotContain("커밋", result.Narrative.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("Generates readable GitHub activity reports", repository.Summary, StringComparison.Ordinal);
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
    public async Task Rule_based_narrative_uses_repository_work_summaries_instead_of_metric_lists()
    {
        var result = await new RuleBasedPublicActivitySummarizer(new SummarySettings
            {
                Language = "ko",
                UsePublicChangeDetails = true
            })
            .SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.Contains(result.Narrative.Highlights, highlight =>
            highlight.Contains("설정 흐름", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Narrative.Highlights, highlight =>
            highlight.Contains("커밋 1건", StringComparison.Ordinal)
            || highlight.Contains("PR 병합 1건", StringComparison.Ordinal));
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
    public void GitHub_copilot_client_rejects_empty_token()
    {
        Assert.Throws<ArgumentException>(() => new GitHubCopilotClient(string.Empty, "auto"));
        Assert.Throws<ArgumentException>(() => new GitHubCopilotClient("   ", null));
    }

    [Fact]
    public void GitHub_copilot_client_defaults_model_to_auto_when_null_or_empty()
    {
        // Verifies construction does not throw for null/empty model.
        // Actual model selection is internal to the SDK.
        var client1 = new GitHubCopilotClient("fine-grained-pat", null);
        var client2 = new GitHubCopilotClient("fine-grained-pat", string.Empty);
        Assert.NotNull(client1);
        Assert.NotNull(client2);
    }

    [Theory]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("```\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("  ```json\n{\"a\":1}\n```  ", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}```", "{\"a\":1}")]
    public void StripMarkdownCodeFences_removes_fences_and_preserves_json(string input, string expected)
    {
        var result = GitHubCopilotClient.StripMarkdownCodeFences(input);
        Assert.Equal(expected, result);
    }


    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task RetryingJsonClient_retries_on_transient_status_codes(int statusCode)
    {
        var callCount = 0;
        var handler = new CallCountingHandler(request =>
        {
            callCount++;
            if (callCount < 3)
            {
                return new HttpResponseMessage((HttpStatusCode)statusCode)
                {
                    Content = new StringContent("retry me", Encoding.UTF8, "text/plain")
                };
            }

            return JsonResponse(
                """{"output":[{"type":"message","content":[{"type":"output_text","text":"{\"summaries\":[]}"}]}]}""");
        });

        var client = new OpenAiResponsesClient("key", "gpt-5.6-sol", new HttpClient(handler), maxRetries: 2);
        // Should NOT throw — retried and succeeded on 3rd attempt
        await client.GenerateAsync(
            new AiTextRequest { Instructions = "inst", Input = "in", MaxOutputTokens = 100 },
            CancellationToken.None);

        Assert.Equal(3, callCount);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public async Task RetryingJsonClient_does_not_retry_non_transient_errors(int statusCode)
    {
        var callCount = 0;
        var handler = new CallCountingHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
            };
        });

        var client = new OpenAiResponsesClient("key", "gpt-5.6-sol", new HttpClient(handler), maxRetries: 3);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GenerateAsync(
                new AiTextRequest { Instructions = "inst", Input = "in", MaxOutputTokens = 100 },
                CancellationToken.None));

        Assert.Equal(1, callCount);
        Assert.Equal((HttpStatusCode)statusCode, ex.StatusCode);
    }

    [Fact]
    public async Task RetryingJsonClient_includes_status_code_and_body_snippet_in_exception_message()
    {
        var handler = new CallCountingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid_api_key", Encoding.UTF8, "text/plain")
        });

        var client = new OpenAiResponsesClient("key", "gpt-5.6-sol", new HttpClient(handler), maxRetries: 0);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GenerateAsync(
                new AiTextRequest { Instructions = "inst", Input = "in", MaxOutputTokens = 100 },
                CancellationToken.None));

        Assert.Contains("400", ex.Message);
        Assert.Contains("invalid_api_key", ex.Message);
    }

    // ── Fallback flag propagation ─────────────────────────────────────────────

    [Fact]
    public async Task Fallback_result_carries_FallbackUsed_true_and_non_null_reason()
    {
        var primary = Substitute.For<GitHubActivityReporter.Core.Abstractions.IPublicActivitySummarizer>();
        primary.SummarizeAsync(Arg.Any<IReadOnlyList<PublicActivityEvent>>(), Arg.Any<CancellationToken>())
            .Returns<GitHubActivityReporter.Core.Models.PublicActivitySummary>(_ =>
                throw new InvalidOperationException("boom"));
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(new SummarySettings()),
            timeout: TimeSpan.FromSeconds(5));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.True(result.FallbackUsed);
        Assert.False(string.IsNullOrWhiteSpace(result.FallbackReason));
    }

    [Fact]
    public async Task Successful_primary_result_has_FallbackUsed_false()
    {
        var client = Substitute.For<IAiTextClient>();
        client.GenerateAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidResponse("summary text"));
        var primary = new AiPublicActivitySummarizer(client, new SummarySettings(), new PublicPrivacySettings());
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(new SummarySettings()),
            timeout: TimeSpan.FromSeconds(5));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.False(result.FallbackUsed);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public async Task HttpRequestException_fallback_reason_contains_status_code()
    {
        var primary = Substitute.For<GitHubActivityReporter.Core.Abstractions.IPublicActivitySummarizer>();
        primary.SummarizeAsync(Arg.Any<IReadOnlyList<PublicActivityEvent>>(), Arg.Any<CancellationToken>())
            .Returns<GitHubActivityReporter.Core.Models.PublicActivitySummary>(_ =>
                throw new HttpRequestException("AI provider returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable));
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(new SummarySettings()),
            timeout: TimeSpan.FromSeconds(5));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.True(result.FallbackUsed);
        Assert.Contains("503", result.FallbackReason);
    }

    [Fact]
    public async Task Timeout_fallback_reason_contains_timeout_seconds()
    {
        var primary = Substitute.For<GitHubActivityReporter.Core.Abstractions.IPublicActivitySummarizer>();
        primary.SummarizeAsync(Arg.Any<IReadOnlyList<PublicActivityEvent>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), callInfo.ArgAt<CancellationToken>(1));
                return new GitHubActivityReporter.Core.Models.PublicActivitySummary
                {
                    Repositories = Array.Empty<GitHubActivityReporter.Core.Models.PublicRepositoryActivity>()
                };
            });
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(new SummarySettings()),
            timeout: TimeSpan.FromMilliseconds(100));

        var result = await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.True(result.FallbackUsed);
        Assert.Contains("timeout", result.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    // ── Secret safety ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("******", "******")]
    [InlineData("token: ABCDEFGHIJKLMNOPQRSTUVWXYZ1234ABCDEFGHIJ", "token: [REDACTED]")]
    [InlineData("normal short message", "normal short message")]
    public void SanitizeMessage_redacts_token_like_sequences(string input, string expected)
    {
        var result = FallbackPublicActivitySummarizer.SanitizeMessage(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Fallback_log_warning_does_not_include_raw_api_key()
    {
        const string fakeKey = "sk-ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEF";
        var primary = Substitute.For<GitHubActivityReporter.Core.Abstractions.IPublicActivitySummarizer>();
        primary.SummarizeAsync(Arg.Any<IReadOnlyList<PublicActivityEvent>>(), Arg.Any<CancellationToken>())
            .Returns<GitHubActivityReporter.Core.Models.PublicActivitySummary>(_ =>
                throw new HttpRequestException($"Unauthorized: key={fakeKey}", null, HttpStatusCode.Unauthorized));
        var log = new GitHubActivityReporter.Core.Abstractions.InMemoryReporterLog();
        var fallback = new FallbackPublicActivitySummarizer(
            primary,
            new RuleBasedPublicActivitySummarizer(new SummarySettings()),
            log,
            timeout: TimeSpan.FromSeconds(5));

        await fallback.SummarizeAsync(PublicEvents(), CancellationToken.None);

        Assert.DoesNotContain(log.Lines, line => line.Contains(fakeKey, StringComparison.Ordinal));
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

    private sealed class CallCountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
