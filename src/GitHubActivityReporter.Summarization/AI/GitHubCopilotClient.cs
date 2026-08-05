#pragma warning disable GHCP001
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace GitHubActivityReporter.Summarization.AI;

/// <summary>
/// GitHub Copilot SDK adapter for constrained public-activity text summarisation.
/// The token is read from configuration and passed explicitly to <see cref="CopilotClientOptions.GitHubToken"/>;
/// the SDK does not fall back to environment variables or local CLI credentials.
/// </summary>
public sealed class GitHubCopilotClient : IAiTextClient
{
    private readonly string _token;
    private readonly string _model;

    private const string PromptPreamble =
        """
        You are a concise technical writer that summarises GitHub activity.
        Respond with a single valid JSON object. Do not include any text before or after the JSON.
        Do not use Markdown code fences. Do not add explanatory sentences.
        Keep every string value on one line (no embedded newlines).
        Use only the supplied input; do not invent facts, names, links, or metrics.
        Do not query external sources.

        """;

    private const string PromptConstraints =
        """

        You MUST follow these rules:
        - Use only the supplied input data.
        - Do not query external sources.
        - Do not invent facts, names, links, or metrics.
        - Do not use Markdown code blocks.
        - Do not add explanatory sentences before or after the JSON.
        - Return exactly one valid JSON object.
        - Keep every string value on a single line with no embedded newlines.
        - Honour the requested language and style.
        - Keep output within the specified length limits.
        """;

    /// <param name="token">Fine-grained PAT with Copilot Requests permission.</param>
    /// <param name="model">Model name to use; pass <see langword="null"/> or empty to use "auto".</param>
    public GitHubCopilotClient(string token, string? model)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("GitHub token is required.", nameof(token));
        }

        _token = token;
        _model = string.IsNullOrWhiteSpace(model) ? "auto" : model;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(AiTextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var prompt = BuildPrompt(request);

        // Each call gets a fresh client and session; both are disposed before returning.
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            GitHubToken = _token,
            UseLoggedInUser = false
        });

        await client.StartAsync(cancellationToken).ConfigureAwait(false);

        var sessionConfig = new SessionConfig
        {
            Model = _model,
            Streaming = false,
            // Empty tool list prevents file access, shell commands, web search, MCP, and sub-agent calls.
            AvailableTools = [],
            // Reject all tool permission requests — this session is for text generation only.
            OnPermissionRequest = (PermissionRequest _, PermissionInvocation _) =>
                Task.FromResult(PermissionDecision.Reject("Tool use is not permitted in summary sessions."))
        };

        await using var session = await client
            .CreateSessionAsync(sessionConfig, cancellationToken)
            .ConfigureAwait(false);

        var result = await session
            .SendAndWaitAsync(prompt, timeout: null, cancellationToken)
            .ConfigureAwait(false);

        var content = result?.Data?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("GitHub Copilot returned an empty response.");
        }

        return content;
    }

    private string BuildPrompt(AiTextRequest request)
        => PromptPreamble + request.Instructions + PromptConstraints + "\n\n[Input]\n\n" + request.Input;
}
