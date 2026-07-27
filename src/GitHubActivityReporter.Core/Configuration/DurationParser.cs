using System.Globalization;
using System.Text.RegularExpressions;

namespace GitHubActivityReporter.Core.Configuration;

/// <summary>Parses compact duration strings such as <c>24h</c>, <c>7d</c>, <c>90m</c>.</summary>
public static partial class DurationParser
{
    public static bool TryParse(string? value, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = DurationRegex().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(match.Groups["amount"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0)
        {
            return false;
        }

        duration = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "m" => TimeSpan.FromMinutes(amount),
            "h" => TimeSpan.FromHours(amount),
            "d" => TimeSpan.FromDays(amount),
            "w" => TimeSpan.FromDays(amount * 7),
            _ => TimeSpan.Zero
        };

        return duration > TimeSpan.Zero;
    }

    public static TimeSpan Parse(string? value)
        => TryParse(value, out var duration)
            ? duration
            : throw new FormatException($"'{value}' is not a valid duration. Use values such as 30m, 24h, 7d or 2w.");

    public static TimeSpan ParseOrDefault(string? value, TimeSpan fallback)
        => TryParse(value, out var duration) ? duration : fallback;

    [GeneratedRegex(@"^(?<amount>\d+(\.\d+)?)\s*(?<unit>[mhdwMHDW])$", RegexOptions.None, 2000)]
    private static partial Regex DurationRegex();
}
