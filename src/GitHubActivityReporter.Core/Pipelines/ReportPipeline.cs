using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Configuration;
using GitHubActivityReporter.Core.Models;
using GitHubActivityReporter.Core.Validation;

namespace GitHubActivityReporter.Core.Pipelines;

public sealed record PipelineOptions
{
    public required ReporterConfiguration Configuration { get; init; }

    public required string WorkingDirectory { get; init; }

    /// <summary>When true no publisher is invoked at all.</summary>
    public bool PreviewMode { get; init; }

    /// <summary>Passed to publishers so they avoid every irreversible side effect.</summary>
    public bool DryRun { get; init; }

    public ValidationContext ValidationContext { get; init; } = new();

    public IReadOnlyDictionary<string, string> PublisherOptions { get; init; }
        = new Dictionary<string, string>();
}

public sealed record PipelineResult
{
    public required IReadOnlyList<RenderedReport> RenderedReports { get; init; }

    public required ValidationResult Validation { get; init; }

    public required IReadOnlyList<PublishResult> PublishResults { get; init; }

    public bool PublishingSkipped { get; init; }

    public bool Succeeded =>
        Validation.IsValid && PublishResults.All(r => r.IsSuccess);
}

/// <summary>
/// Render → validate → publish. Publishers are only reached when the privacy
/// validation passed and the pipeline is not running in preview mode.
/// </summary>
public sealed class ReportPipeline
{
    private readonly IReadOnlyList<IReportRenderer> _renderers;
    private readonly IReadOnlyList<IReportPublisher> _publishers;
    private readonly IOutputValidator _validator;
    private readonly IReporterLog _log;

    public ReportPipeline(
        IEnumerable<IReportRenderer> renderers,
        IEnumerable<IReportPublisher> publishers,
        IOutputValidator validator,
        IReporterLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        ArgumentNullException.ThrowIfNull(publishers);

        _renderers = renderers.ToArray();
        _publishers = publishers.ToArray();
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _log = log ?? NullReporterLog.Instance;
    }

    public async Task<PipelineResult> ExecuteAsync(
        ActivityReport report,
        PipelineOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        var rendered = new List<RenderedReport>();
        foreach (var renderer in _renderers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new RendererContext
            {
                Configuration = options.Configuration,
                TimeZone = options.Configuration.Schedule.ResolveTimeZone(),
                TargetPath = OutputTargetResolver.Resolve(options.Configuration, renderer.RendererId),
                IsPreview = options.PreviewMode
            };

            rendered.Add(await renderer.RenderAsync(report, context, cancellationToken).ConfigureAwait(false));
        }

        _log.Info($"Generated {rendered.Sum(r => r.Artifacts.Count)} outputs.");

        var validation = ValidationResult.Success();
        foreach (var renderedReport in rendered)
        {
            validation = validation.Merge(_validator.Validate(renderedReport, options.ValidationContext));
        }

        if (!validation.IsValid)
        {
            foreach (var issue in validation.Errors)
            {
                _log.Error(issue.ToString());
            }

            _log.Error("Privacy validation failed. Publishing has been cancelled.");
            return new PipelineResult
            {
                RenderedReports = rendered,
                Validation = validation,
                PublishResults = Array.Empty<PublishResult>(),
                PublishingSkipped = true
            };
        }

        foreach (var warning in validation.Warnings)
        {
            _log.Warning(warning.ToString());
        }

        _log.Info($"Validated {rendered.Sum(r => r.Artifacts.Count)} outputs.");

        if (options.PreviewMode)
        {
            _log.Info("Preview mode: publishing was not executed.");
            return new PipelineResult
            {
                RenderedReports = rendered,
                Validation = validation,
                PublishResults = Array.Empty<PublishResult>(),
                PublishingSkipped = true
            };
        }

        var publisherContext = new PublisherContext
        {
            Configuration = options.Configuration,
            WorkingDirectory = options.WorkingDirectory,
            DryRun = options.DryRun,
            Options = options.PublisherOptions
        };

        var publishResults = new List<PublishResult>();
        foreach (var publisher in _publishers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var renderedReport in rendered)
            {
                var result = await publisher
                    .PublishAsync(renderedReport, publisherContext, cancellationToken)
                    .ConfigureAwait(false);

                publishResults.Add(result);

                if (!result.IsSuccess)
                {
                    _log.Error($"Publisher '{publisher.PublisherId}' failed: {result.Message}");
                }
            }
        }

        _log.Info($"Published {publishResults.Count(r => r.Outcome == PublishOutcome.Published)} outputs.");

        return new PipelineResult
        {
            RenderedReports = rendered,
            Validation = validation,
            PublishResults = publishResults
        };
    }
}

/// <summary>Maps a renderer id to the configured output path.</summary>
public static class OutputTargetResolver
{
    public static string? Resolve(ReporterConfiguration configuration, string rendererId)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return rendererId switch
        {
            KnownRenderers.CompactMarkdown => configuration.Outputs.GitHubProfile.Target,
            KnownRenderers.NormalizedJson => configuration.Outputs.Json.Target,
            KnownRenderers.SvgDashboard => configuration.Outputs.Dashboard.Target,
            KnownRenderers.StaticHtml => Path.Combine(configuration.Outputs.Website.OutputDirectory, "index.html"),
            _ => null
        };
    }
}
