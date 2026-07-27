namespace GitHubActivityReporter.Summarization.AI;

public sealed record AiTextRequest
{
    public required string Instructions { get; init; }
    public required string Input { get; init; }
    public required int MaxOutputTokens { get; init; }
}

public interface IAiTextClient
{
    Task<string> GenerateAsync(AiTextRequest request, CancellationToken cancellationToken);
}
