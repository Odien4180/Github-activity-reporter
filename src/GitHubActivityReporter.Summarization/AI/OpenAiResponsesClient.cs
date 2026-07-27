using System.Text.Json;

namespace GitHubActivityReporter.Summarization.AI;

/// <summary>Minimal OpenAI Responses API client for constrained JSON summaries.</summary>
public sealed class OpenAiResponsesClient : RetryingJsonClient, IAiTextClient
{
    private static readonly Uri Endpoint = new("https://api.openai.com/v1/responses");
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiResponsesClient(string apiKey, string model, HttpClient? httpClient = null, int maxRetries = 2)
        : base(httpClient, maxRetries)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("API key is required.", nameof(apiKey)) : apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("Model is required.", nameof(model)) : model;
    }

    public async Task<string> GenerateAsync(AiTextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            model = _model,
            instructions = request.Instructions,
            input = request.Input,
            max_output_tokens = request.MaxOutputTokens,
            store = false,
            reasoning = new { effort = "low" },
            text = new
            {
                verbosity = "low",
                format = new
                {
                    type = "json_schema",
                    name = "public_activity_summaries",
                    strict = true,
                    schema = SummarySchema.Value
                }
            }
        };

        using var response = await PostAsync(Endpoint, _apiKey, payload, null, cancellationToken).ConfigureAwait(false);
        return ExtractOutputText(response.RootElement)
               ?? throw new InvalidOperationException("OpenAI response did not contain output text.");
    }

    internal static string? ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}

internal static class SummarySchema
{
    public static object Value { get; } = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            headline = new { type = "string", maxLength = 200 },
            highlights = new
            {
                type = "array",
                minItems = 1,
                maxItems = 5,
                items = new { type = "string", maxLength = 300 }
            },
            summaries = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        id = new { type = "string" },
                        summary = new { type = "string", maxLength = 300 }
                    },
                    required = new[] { "id", "summary" }
                }
            }
        },
        required = new[] { "headline", "highlights", "summaries" }
    };
}
