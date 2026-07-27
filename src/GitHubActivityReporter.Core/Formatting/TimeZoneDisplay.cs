using System.Globalization;

namespace GitHubActivityReporter.Core.Formatting;

/// <summary>Formats timestamps for human readable outputs.</summary>
public static class TimeZoneDisplay
{
    private static readonly Dictionary<string, (string Standard, string Daylight)> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Seoul"] = ("KST", "KST"),
        ["Asia/Tokyo"] = ("JST", "JST"),
        ["Asia/Shanghai"] = ("CST", "CST"),
        ["Asia/Kolkata"] = ("IST", "IST"),
        ["UTC"] = ("UTC", "UTC"),
        ["Etc/UTC"] = ("UTC", "UTC"),
        ["Europe/London"] = ("GMT", "BST"),
        ["Europe/Berlin"] = ("CET", "CEST"),
        ["Europe/Paris"] = ("CET", "CEST"),
        ["America/New_York"] = ("EST", "EDT"),
        ["America/Chicago"] = ("CST", "CDT"),
        ["America/Denver"] = ("MST", "MDT"),
        ["America/Los_Angeles"] = ("PST", "PDT"),
        ["Australia/Sydney"] = ("AEST", "AEDT")
    };

    public static string Abbreviate(TimeZoneInfo timeZone, DateTimeOffset moment)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var local = TimeZoneInfo.ConvertTime(moment, timeZone);
        var isDaylight = timeZone.IsDaylightSavingTime(local);

        if (Abbreviations.TryGetValue(timeZone.Id, out var abbreviation))
        {
            return isDaylight ? abbreviation.Daylight : abbreviation.Standard;
        }

        var offset = local.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"UTC{sign}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}");
    }

    /// <summary>Formats as <c>2026-07-27 09:00 KST</c>.</summary>
    public static string FormatLocal(DateTimeOffset moment, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var local = TimeZoneInfo.ConvertTime(moment, timeZone);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{local:yyyy-MM-dd HH:mm} {Abbreviate(timeZone, moment)}");
    }

    /// <summary>Formats as <c>2026-07-26</c> in the given zone.</summary>
    public static string FormatLocalDate(DateTimeOffset moment, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = TimeZoneInfo.ConvertTime(moment, timeZone);
        return local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
