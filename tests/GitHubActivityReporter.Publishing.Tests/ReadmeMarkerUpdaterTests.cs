using GitHubActivityReporter.Publishing.GitHubProfile;

namespace GitHubActivityReporter.Publishing.Tests;

public sealed class ReadmeMarkerUpdaterTests
{
    [Fact]
    public void Update_preserves_user_content_outside_generated_block()
    {
        var existing = $"# Profile\n\nUser introduction\n\n{ReadmeMarkerUpdater.StartMarker}\nold\n{ReadmeMarkerUpdater.EndMarker}\n\nFooter\n";

        var result = ReadmeMarkerUpdater.Update(existing, "new activity");

        Assert.True(result.Changed);
        Assert.Equal(ReadmeUpdateOutcome.SectionReplaced, result.Outcome);
        Assert.StartsWith("# Profile\n\nUser introduction", result.Content, StringComparison.Ordinal);
        Assert.Contains("new activity", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("old", result.Content, StringComparison.Ordinal);
        Assert.EndsWith("\n\nFooter\n", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_rejects_unbalanced_markers()
    {
        var exception = Assert.Throws<ReadmeMarkerException>(
            () => ReadmeMarkerUpdater.Update($"# Profile\n{ReadmeMarkerUpdater.StartMarker}", "activity"));

        Assert.Contains("unbalanced", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
