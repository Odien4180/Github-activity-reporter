using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitHubActivityReporter.Core.Configuration;

/// <summary>Loads and saves <c>activity-reporter.yml</c>.</summary>
public sealed class ConfigurationLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static ConfigurationLoader Default { get; } = new();

    public ReporterConfiguration Deserialize(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var configuration = _deserializer.Deserialize<ReporterConfiguration>(yaml);
        return configuration ?? throw new InvalidOperationException("Configuration file is empty.");
    }

    public string Serialize(ReporterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return _serializer.Serialize(configuration);
    }

    public async Task<ReporterConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}", path);
        }

        var yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(yaml);
    }

    public async Task SaveAsync(ReporterConfiguration configuration, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yaml = Serialize(configuration);
        await File.WriteAllTextAsync(path, yaml, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds the configuration file walking up from <paramref name="startDirectory"/>.</summary>
    public static string? FindConfigurationFile(string startDirectory, string fileName = ReporterConfiguration.DefaultFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
