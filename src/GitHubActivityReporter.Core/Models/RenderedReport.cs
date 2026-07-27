namespace GitHubActivityReporter.Core.Models;

/// <summary>Kind of a rendered artifact, used by validators and publishers.</summary>
public enum RenderedArtifactKind
{
    Markdown,
    Json,
    Svg,
    Html,
    Css,
    JavaScript,
    PlainText
}

/// <summary>A single file produced by a renderer.</summary>
public sealed record RenderedArtifact
{
    public required string Name { get; init; }

    /// <summary>Path relative to the output root, e.g. <c>generated/activity.md</c>.</summary>
    public required string RelativePath { get; init; }

    public required string Content { get; init; }

    public required RenderedArtifactKind Kind { get; init; }
}

/// <summary>All files produced by one renderer.</summary>
public sealed record RenderedReport
{
    public required string RendererId { get; init; }

    public required IReadOnlyList<RenderedArtifact> Artifacts { get; init; }

    public RenderedArtifact? PrimaryArtifact => Artifacts.Count > 0 ? Artifacts[0] : null;
}
