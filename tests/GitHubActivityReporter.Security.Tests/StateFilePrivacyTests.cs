using GitHubActivityReporter.Core.State;

namespace GitHubActivityReporter.Security.Tests;

public sealed class StateFilePrivacyTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        AppContext.BaseDirectory,
        "state-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task State_file_only_contains_the_documented_fields()
    {
        var store = new FileReporterStateStore(_directory);

        await store.SaveAsync(
            new ReporterState
            {
                LastSuccessfulRunAt = SampleActivity.PeriodEnd,
                LastReportHash = "sha256-abc"
            },
            CancellationToken.None);

        var json = await File.ReadAllTextAsync(store.StatePath);

        Assert.Contains("schemaVersion", json, StringComparison.Ordinal);
        Assert.Contains("reporterVersion", json, StringComparison.Ordinal);
        Assert.Contains("lastSuccessfulRunAt", json, StringComparison.Ordinal);
        Assert.Contains("lastReportHash", json, StringComparison.Ordinal);

        foreach (var forbidden in SampleActivity.PrivateStrings)
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task State_file_never_stores_the_opaque_private_identifier()
    {
        var (collected, _) = SampleActivity.Collect();
        var store = new FileReporterStateStore(_directory);

        await store.SaveAsync(
            new ReporterState { LastSuccessfulRunAt = SampleActivity.PeriodEnd, LastReportHash = "sha256-abc" },
            CancellationToken.None);

        var json = await File.ReadAllTextAsync(store.StatePath);

        foreach (var id in collected.PrivateEvents.Select(e => e.RepositoryOpaqueId).Distinct())
        {
            Assert.DoesNotContain(id, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task State_round_trips()
    {
        var store = new FileReporterStateStore(_directory);
        var state = new ReporterState
        {
            LastSuccessfulRunAt = SampleActivity.PeriodEnd,
            LastReportHash = "sha256-abc"
        };

        await store.SaveAsync(state, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(state.LastSuccessfulRunAt, loaded!.LastSuccessfulRunAt);
        Assert.Equal(state.LastReportHash, loaded.LastReportHash);
        Assert.Equal(ReporterState.CurrentSchemaVersion, loaded.SchemaVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
