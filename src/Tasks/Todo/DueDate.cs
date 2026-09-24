using System.Globalization;
using System.Text.RegularExpressions;

namespace Tasks.Todo;

/// <summary>
/// Turns what a person types after <c>--due</c> into a date. The line always stores the
/// absolute date, so a relative value means the same thing whenever the file is read.
/// </summary>
internal static partial class DueDate
{
    [GeneratedRegex(@"^\+(\d{1,4})([dw])$")]
    private static partial Regex OffsetRegex();

    private static readonly Dictionary<string, DayOfWeek> _weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = DayOfWeek.Monday,
        ["monday"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["tuesday"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["wednesday"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["thursday"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["friday"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["saturday"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday,
        ["sunday"] = DayOfWeek.Sunday,
    };

    /// <summary>
    /// <c>yyyy-MM-dd</c>, <c>today</c>, <c>tomorrow</c>, <c>+3d</c>, <c>+2w</c>, or a weekday
    /// name - the next one strictly after today. Null when the value is none of these.
    /// </summary>
    internal static DateOnly? Parse(string raw, DateOnly today)
    {
        var value = raw.Trim();

        if (DateOnly.TryParseExact(value, TodoLine.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (value.Equals("today", StringComparison.OrdinalIgnoreCase))
        {
            return today;
        }

        if (value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            return today.AddDays(1);
        }

        var offset = OffsetRegex().Match(value);
        if (offset.Success)
        {
            var count = int.Parse(offset.Groups[1].Value, CultureInfo.InvariantCulture);
            return today.AddDays(offset.Groups[2].Value == "w" ? count * 7 : count);
        }

        if (_weekdays.TryGetValue(value, out var weekday))
        {
            var days = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(days == 0 ? 7 : days);
        }

        return null;
    }
}
