using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Publishing.Tests;

internal static class PublisherTestData
{
    public static RenderedReport MarkdownReport(string content = "generated activity") => new()
    {
        RendererId = KnownRenderers.CompactMarkdown,
        Artifacts =
        [
            new RenderedArtifact
            {
                Name = "activity",
                RelativePath = "generated/activity.md",
                Content = content,
                Kind = RenderedArtifactKind.Markdown
            }
        ]
    };

    public static PublisherContext Context(
        string workingDirectory,
        ReporterConfiguration? configuration = null,
        bool dryRun = false,
        IReadOnlyDictionary<string, string>? options = null) => new()
    {
        Configuration = configuration ?? ReporterConfiguration.CreateDefault("example-user"),
        WorkingDirectory = workingDirectory,
        DryRun = dryRun,
        Options = options ?? new Dictionary<string, string>()
    };
}
