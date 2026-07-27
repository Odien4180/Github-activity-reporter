using System.Globalization;
using GitHubActivityReporter.Core.Configuration;

namespace GitHubActivityReporter.Bootstrap.GitHubActions;

/// <summary>
/// Converts the user's local schedule into a UTC cron expression, because GitHub
/// Actions always evaluates <c>schedule.cron</c> in UTC.
/// </summary>
public static class CronExpressionGenerator
{
    /// <summary>Returns the cron expression, or null when the schedule is manual only.</summary>
    public static string? Generate(string localTime, string timezoneId, ScheduleFrequency frequency)
    {
        if (frequency == ScheduleFrequency.Manual)
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(localTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new FormatException($"'{localTime}' is not a valid HH:mm local time.");
        }

        var timeZone = ResolveTimeZone(timezoneId);

        // Reference Monday used to compute the UTC offset and the possible day shift.
        var referenceLocal = new DateTime(2026, 1, 5, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(referenceLocal);
        var utc = new DateTimeOffset(referenceLocal, offset).UtcDateTime;

        var shift = DateOnly.FromDateTime(utc).DayNumber - DateOnly.FromDateTime(referenceLocal).DayNumber;

        var minute = utc.Minute.ToString(CultureInfo.InvariantCulture);
        var hour = utc.Hour.ToString(CultureInfo.InvariantCulture);

        return frequency switch
        {
            ScheduleFrequency.Daily => $"{minute} {hour} * * *",
            ScheduleFrequency.Weekdays => $"{minute} {hour} * * {ShiftDays([1, 2, 3, 4, 5], shift)}",
            ScheduleFrequency.Weekly => $"{minute} {hour} * * {ShiftDays([1], shift)}",
            _ => $"{minute} {hour} * * *"
        };
    }

    public static string? Generate(ScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        return schedule.Enabled
            ? Generate(schedule.LocalTime, schedule.Timezone, schedule.Frequency)
            : null;
    }

    /// <summary>Describes the schedule in a human readable, safe way.</summary>
    public static string Describe(ScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (!schedule.Enabled || schedule.Frequency == ScheduleFrequency.Manual)
        {
            return "manual runs only (workflow_dispatch)";
        }

        var cron = Generate(schedule);
        return $"{schedule.Frequency.ToString().ToLowerInvariant()} at {schedule.LocalTime} {schedule.Timezone} (cron: {cron} UTC)";
    }

    private static string ShiftDays(int[] days, int shift)
    {
        var shifted = days
            .Select(day => ((day + shift) % 7 + 7) % 7)
            .Distinct()
            .OrderBy(day => day)
            .Select(day => day.ToString(CultureInfo.InvariantCulture));

        return string.Join(',', shifted);
    }

    private static TimeZoneInfo ResolveTimeZone(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"'{timezoneId}' is not a known IANA time zone.", nameof(timezoneId));
        }
    }
}
