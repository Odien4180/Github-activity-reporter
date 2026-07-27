using System.Text.Json;

namespace GitHubActivityReporter.Summarization.AI;

/// <summary>GitHub Models inference adapter, usable from Copilot-enabled GitHub workflows.</summary>
public sealed class GitHubModelsClient : RetryingJsonClient, IAiTextClient
{
    private static readonly Uri Endpoint = new("https://models.github.ai/inference/chat/completions");
    private readonly string _token;
    private readonly string _model;

    public GitHubModelsClient(string token, string model, HttpClient? httpClient = null, int maxRetries = 2)
        : base(httpClient, maxRetries)
    {
        _token = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("GitHub token is required.", nameof(token)) : token;
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("Model is required.", nameof(model)) : model;
    }

    public async Task<string> GenerateAsync(AiTextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = request.Instructions },
                new { role = "user", content = request.Input }
            },
            max_tokens = request.MaxOutputTokens,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "public_activity_summaries",
                    strict = true,
                    schema = SummarySchema.Value
                }
            }
        };

        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/vnd.github+json",
            ["X-GitHub-Api-Version"] = "2026-03-10"
        };
        using var response = await PostAsync(Endpoint, _token, payload, headers, cancellationToken).ConfigureAwait(false);

        if (response.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content))
        {
            return content.GetString()
                   ?? throw new InvalidOperationException("GitHub Models response contained empty content.");
        }

        throw new InvalidOperationException("GitHub Models response did not contain a chat completion.");
    }
}
