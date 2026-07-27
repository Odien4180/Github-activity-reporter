using System.Text;

namespace GitHubActivityReporter.Publishing.GitHubProfile;

public enum ReadmeUpdateOutcome
{
    Created,
    MarkersAppended,
    SectionReplaced,
    Unchanged
}

public sealed record ReadmeUpdateResult
{
    public required string Content { get; init; }

    public required bool Changed { get; init; }

    public required ReadmeUpdateOutcome Outcome { get; init; }
}

/// <summary>Raised when the README markers are unusable. Publishing must stop.</summary>
public sealed class ReadmeMarkerException : Exception
{
    public ReadmeMarkerException(string message) : base(message)
    {
    }
}

/// <summary>
/// Replaces only the generated block of a profile README.
/// Everything the user wrote outside the markers is preserved byte for byte.
/// </summary>
public static class ReadmeMarkerUpdater
{
    public const string StartMarker = "<!-- GITHUB_ACTIVITY_REPORTER:START -->";
    public const string EndMarker = "<!-- GITHUB_ACTIVITY_REPORTER:END -->";

    public static ReadmeUpdateResult Update(string? existingReadme, string generatedContent)
    {
        ArgumentNullException.ThrowIfNull(generatedContent);

        var block = BuildBlock(generatedContent);

        if (string.IsNullOrWhiteSpace(existingReadme))
        {
            return new ReadmeUpdateResult
            {
                Content = block,
                Changed = true,
                Outcome = ReadmeUpdateOutcome.Created
            };
        }

        var readme = existingReadme!;
        var startCount = CountOccurrences(readme, StartMarker);
        var endCount = CountOccurrences(readme, EndMarker);

        if (startCount > 1 || endCount > 1)
        {
            throw new ReadmeMarkerException(
                "The README contains duplicated GITHUB_ACTIVITY_REPORTER markers. Remove the duplicates and run again.");
        }

        if (startCount != endCount)
        {
            throw new ReadmeMarkerException(
                "The README contains an unbalanced GITHUB_ACTIVITY_REPORTER marker pair.");
        }

        if (startCount == 0)
        {
            var appended = new StringBuilder(readme.TrimEnd('\r', '\n'));
            appended.AppendLine();
            appended.AppendLine();
            appended.Append(block);

            return new ReadmeUpdateResult
            {
                Content = appended.ToString(),
                Changed = true,
                Outcome = ReadmeUpdateOutcome.MarkersAppended
            };
        }

        var startIndex = readme.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIndex = readme.IndexOf(EndMarker, StringComparison.Ordinal);

        if (endIndex < startIndex)
        {
            throw new ReadmeMarkerException(
                "The GITHUB_ACTIVITY_REPORTER end marker appears before the start marker.");
        }

        var prefix = readme[..startIndex];
        var suffix = readme[(endIndex + EndMarker.Length)..];
        var updated = prefix + block.TrimEnd('\r', '\n') + suffix;

        return new ReadmeUpdateResult
        {
            Content = updated,
            Changed = !string.Equals(updated, readme, StringComparison.Ordinal),
            Outcome = string.Equals(updated, readme, StringComparison.Ordinal)
                ? ReadmeUpdateOutcome.Unchanged
                : ReadmeUpdateOutcome.SectionReplaced
        };
    }

    public static bool HasMarkers(string? readme)
        => readme is not null
           && readme.Contains(StartMarker, StringComparison.Ordinal)
           && readme.Contains(EndMarker, StringComparison.Ordinal);

    private static string BuildBlock(string generatedContent)
    {
        var builder = new StringBuilder();
        builder.AppendLine(StartMarker);
        builder.AppendLine();
        builder.AppendLine(generatedContent.Trim('\r', '\n'));
        builder.AppendLine();
        builder.AppendLine(EndMarker);
        return builder.ToString();
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
