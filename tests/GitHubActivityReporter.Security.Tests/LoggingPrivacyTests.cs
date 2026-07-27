using GitHubActivityReporter.Core.Abstractions;
using GitHubActivityReporter.Core.Security;

namespace GitHubActivityReporter.Security.Tests;

public sealed class LoggingPrivacyTests
{
    /// <summary>Token shaped, but deliberately built at runtime so no literal credential exists in the repository.</summary>
    private static readonly string TokenShapedValue = "gh" + "p_" + new string('x', 36);

    private const string SecretValue = "not-a-real-credential-value";

    [Fact]
    public void Known_secrets_are_masked_in_every_log_level()
    {
        var sink = new InMemoryReporterLog();
        var log = new MaskingReporterLog(sink, secrets: [SecretValue]);

        log.Debug($"using credential {SecretValue}");
        log.Info($"using credential {SecretValue}");
        log.Warning($"using credential {SecretValue}");
        log.Error($"using credential {SecretValue}");

        Assert.Equal(4, sink.Lines.Count);
        foreach (var line in sink.Lines)
        {
            Assert.DoesNotContain(SecretValue, line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Token_shaped_values_are_masked_even_when_unknown()
    {
        var sink = new InMemoryReporterLog();
        var log = new MaskingReporterLog(sink);

        log.Info($"authorization: {TokenShapedValue}");

        Assert.DoesNotContain(TokenShapedValue, sink.Lines[0], StringComparison.Ordinal);
        Assert.Contains(SecretMasker.FullMask, sink.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Private_identifiers_are_masked_in_logs_including_debug()
    {
        var (_, registry) = SampleActivity.Collect();
        var sink = new InMemoryReporterLog();
        var log = new MaskingReporterLog(sink, registry);

        log.Debug($"collected activity from {SampleActivity.PrivateRepository} ({SampleActivity.PrivatePullRequestTitle})");
        log.Info($"organization {SampleActivity.PrivateOrganization} had activity");

        Assert.Equal(2, sink.Lines.Count);
        foreach (var line in sink.Lines)
        {
            foreach (var forbidden in SampleActivity.PrivateStrings)
            {
                Assert.DoesNotContain(forbidden, line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Sanitize_can_be_used_for_exception_messages()
    {
        var (_, registry) = SampleActivity.Collect();
        var log = new MaskingReporterLog(new InMemoryReporterLog(), registry, [SecretValue]);

        var message = log.Sanitize($"Failed to read {SampleActivity.PrivateRepository} using {SecretValue}");

        Assert.DoesNotContain(SampleActivity.PrivateRepository, message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretValue, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registry_tostring_does_not_reveal_terms()
    {
        var (_, registry) = SampleActivity.Collect();

        var text = registry.ToString();

        foreach (var forbidden in SampleActivity.PrivateStrings)
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
