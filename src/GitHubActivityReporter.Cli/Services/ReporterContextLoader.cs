using FluentValidation.Results;
using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Cli.Services;

public sealed record LoadedConfiguration
{
    public required ReporterConfiguration Configuration { get; init; }

    public required string Path { get; init; }
}

public sealed class ConfigurationLoadException : Exception
{
    public ConfigurationLoadException(string message, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        Errors = errors ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Finds, loads and validates the configuration for every command.</summary>
public sealed class ReporterContextLoader
{
    private readonly ConfigurationLoader _loader;
    private readonly ReporterConfigurationValidator _validator = new();

    public ReporterContextLoader(ConfigurationLoader? loader = null)
    {
        _loader = loader ?? ConfigurationLoader.Default;
    }

    public string? Locate(string? explicitPath, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath!);
        }

        return ConfigurationLoader.FindConfigurationFile(workingDirectory);
    }

    public async Task<LoadedConfiguration> LoadAsync(
        string? explicitPath,
        string workingDirectory,
        bool validate = true,
        CancellationToken cancellationToken = default)
    {
        var path = Locate(explicitPath, workingDirectory);
        if (path is null || !File.Exists(path))
        {
            throw new ConfigurationLoadException(
                $"No {ReporterConfiguration.DefaultFileName} was found. Run 'github-activity-reporter init' first.");
        }

        ReporterConfiguration configuration;
        try
        {
            configuration = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidOperationException)
        {
            throw new ConfigurationLoadException($"The configuration file could not be parsed: {exception.Message}");
        }

        if (validate)
        {
            ValidationResult result = _validator.Validate(configuration);
            if (!result.IsValid)
            {
                throw new ConfigurationLoadException(
                    "The configuration file is not valid.",
                    result.Errors.Select(e => e.ErrorMessage).ToArray());
            }
        }

        return new LoadedConfiguration { Configuration = configuration, Path = path };
    }

    public IReadOnlyList<string> Validate(ReporterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var result = _validator.Validate(configuration);
        return result.IsValid
            ? Array.Empty<string>()
            : result.Errors.Select(e => e.ErrorMessage).ToArray();
    }
}
