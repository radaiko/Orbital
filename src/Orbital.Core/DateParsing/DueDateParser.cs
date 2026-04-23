// src/Orbital.Core/DateParsing/DueDateParser.cs
namespace Orbital.Core.DateParsing;

using System.Globalization;
using System.Text.RegularExpressions;

public sealed class DueDateParser
{
    private readonly Func<DateOnly> today;

    public DueDateParser() : this(() => DateOnly.FromDateTime(DateTime.Today)) { }

    public DueDateParser(Func<DateOnly> todayProvider)
    {
        today = todayProvider;
    }

    public ParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ParseResult.Empty();

        var s = input.Trim().ToLowerInvariant();

        // Rule 1: relative literals
        switch (s)
        {
            case "today": return ParseResult.Ok(today());
            case "tomorrow":
            case "tmr":
                return ParseResult.Ok(today().AddDays(1));
        }

        // Rule 2: "in N days" / "in N weeks"
        var inMatch = Regex.Match(s, @"^in\s+(-?\d+)\s+(day|days|week|weeks)$");
        if (inMatch.Success)
        {
            var n = int.Parse(inMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n <= 0) return ParseResult.Error("must be positive");
            var unit = inMatch.Groups[2].Value;
            var days = unit.StartsWith("week", StringComparison.Ordinal) ? n * 7 : n;
            return ParseResult.Ok(today().AddDays(days));
        }

        // Rule 3: ISO date — yyyy-m-d or yyyy/m/d
        var isoMatch = Regex.Match(s, @"^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$");
        if (isoMatch.Success)
        {
            if (TryBuildDate(int.Parse(isoMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                             int.Parse(isoMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                             int.Parse(isoMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                             out var iso))
                return ParseResult.Ok(iso);
            return ParseResult.Error("invalid ISO date");
        }

        // Rule 4: "next ..."
        if (s.StartsWith("next ", StringComparison.Ordinal))
        {
            var rest = s[5..].Trim();
            if (rest == "week")
                return ParseResult.Ok(NextWeekday(DayOfWeek.Monday, skipIfToday: true));
            if (rest == "month")
                return ParseResult.Ok(AddMonthsClamped(today(), 1));
            if (TryParseWeekday(rest, out var dow))
                return ParseResult.Ok(NextWeekday(dow, skipIfToday: true).AddDays(7).AddDays(-0));
            return ParseResult.Error($"don't understand 'next {rest}'");
        }

        // Rule 5: weekday
        if (TryParseWeekday(s, out var wd))
            return ParseResult.Ok(NextWeekday(wd, skipIfToday: false));

        // Rule 6: short day.month with . / -
        var shortMatch = Regex.Match(s, @"^(\d{1,2})[./-](\d{1,2})$");
        if (shortMatch.Success)
        {
            var day = int.Parse(shortMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(shortMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var t = today();
            if (TryBuildDate(t.Year, month, day, out var thisYear))
            {
                if (thisYear < t) return ParseResult.Ok(new DateOnly(t.Year + 1, month, day));
                return ParseResult.Ok(thisYear);
            }
            return ParseResult.Error("invalid date");
        }

        return ParseResult.Error("unrecognized");
    }

    // Helpers --------------------------------------------------------------

    private static bool TryBuildDate(int year, int month, int day, out DateOnly date)
    {
        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private DateOnly NextWeekday(DayOfWeek target, bool skipIfToday)
    {
        var t = today();
        var diff = ((int)target - (int)t.DayOfWeek + 7) % 7;
        if (diff == 0 && skipIfToday) diff = 7;
        return t.AddDays(diff);
    }

    private static DateOnly AddMonthsClamped(DateOnly d, int months)
    {
        var year = d.Year + (d.Month - 1 + months) / 12;
        var month = (d.Month - 1 + months) % 12 + 1;
        var lastDay = DateTime.DaysInMonth(year, month);
        var day = Math.Min(d.Day, lastDay);
        return new DateOnly(year, month, day);
    }

    private static bool TryParseWeekday(string input, out DayOfWeek day)
    {
        day = input switch
        {
            "mon" or "monday" => DayOfWeek.Monday,
            "tue" or "tues" or "tuesday" => DayOfWeek.Tuesday,
            "wed" or "wednesday" => DayOfWeek.Wednesday,
            "thu" or "thur" or "thurs" or "thursday" => DayOfWeek.Thursday,
            "fri" or "friday" => DayOfWeek.Friday,
            "sat" or "saturday" => DayOfWeek.Saturday,
            "sun" or "sunday" => DayOfWeek.Sunday,
            _ => (DayOfWeek)(-1),
        };
        return (int)day >= 0;
    }
}
