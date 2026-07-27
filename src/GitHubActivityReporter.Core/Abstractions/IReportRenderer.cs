using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;

namespace GitHubActivityReporter.Core.Abstractions;

public sealed record RendererContext
{
    public required ReporterConfiguration Configuration { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }

    /// <summary>Target path relative to the output root for the primary artifact.</summary>
    public string? TargetPath { get; init; }

    public bool IsPreview { get; init; }

    public string Language => Configuration.Summary.Language;

    public static RendererContext ForConfiguration(ReporterConfiguration configuration, bool isPreview = false)
        => new()
        {
            Configuration = configuration,
            TimeZone = configuration.Schedule.ResolveTimeZone(),
            IsPreview = isPreview
        };
}

public interface IReportRenderer
{
    string RendererId { get; }

    Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken);
}
