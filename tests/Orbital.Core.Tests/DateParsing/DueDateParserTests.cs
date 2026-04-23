// tests/Orbital.Core.Tests/DateParsing/DueDateParserTests.cs
namespace Orbital.Core.Tests.DateParsing;

using FluentAssertions;
using Orbital.Core.DateParsing;
using Xunit;

public sealed class DueDateParserTests
{
    // Fixed "today" for deterministic tests — a Thursday so weekday rollover is visible.
    private static readonly DateOnly Today = new(2026, 4, 23); // 2026-04-23 is a Thursday
    private readonly DueDateParser parser = new(() => Today);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_returns_Empty(string input)
    {
        var r = parser.Parse(input);
        r.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData("today",    2026, 4, 23)]
    [InlineData("tomorrow", 2026, 4, 24)]
    [InlineData("tmr",      2026, 4, 24)]
    [InlineData("TODAY",    2026, 4, 23)] // case-insensitive
    public void Relative_literals(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("in 1 day",   2026, 4, 24)]
    [InlineData("in 3 days",  2026, 4, 26)]
    [InlineData("in 1 week",  2026, 4, 30)]
    [InlineData("in 2 weeks", 2026, 5, 7)]
    public void In_N_days_and_weeks(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("mon",     2026, 4, 27)] // next Monday (today is Thu)
    [InlineData("monday",  2026, 4, 27)]
    [InlineData("thu",     2026, 4, 23)] // today
    [InlineData("fri",     2026, 4, 24)]
    [InlineData("sunday",  2026, 4, 26)]
    public void Weekday_resolves_to_next_occurrence(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("next mon",   2026, 5, 4)]   // Monday after this one
    [InlineData("next week",  2026, 4, 27)]  // next Monday
    [InlineData("next month", 2026, 5, 23)]  // same day next month
    public void Next_X(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("2026-04-25", 2026, 4, 25)]
    [InlineData("2026/04/25", 2026, 4, 25)]
    [InlineData("2026-4-25",  2026, 4, 25)]  // permissive single-digit month/day
    public void Iso_dates(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("25.4",   2026, 4, 25)]   // future this year
    [InlineData("25/4",   2026, 4, 25)]
    [InlineData("25-04",  2026, 4, 25)]
    [InlineData("1.1",    2027, 1, 1)]    // already past → next year
    [InlineData("22.4",   2027, 4, 22)]   // 22 Apr 2026 is past → next year
    public void Short_day_month(string input, int y, int m, int d)
    {
        var r = parser.Parse(input);
        r.Date.Should().Be(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("asdf")]
    [InlineData("99.99")]
    [InlineData("in 0 days")]
    [InlineData("in -1 days")]
    [InlineData("2026-13-01")]
    [InlineData("next yesterday")]
    public void Invalid_inputs_return_error(string input)
    {
        var r = parser.Parse(input);
        r.IsError.Should().BeTrue();
    }

    [Fact]
    public void Next_weekday_when_today_is_target_returns_one_week_not_two()
    {
        // Regression: when today IS the target weekday, "next <weekday>" must
        // resolve to today + 7, not today + 14.
        var monday = new DateOnly(2026, 4, 27);
        var p = new DueDateParser(() => monday);
        p.Parse("next mon").Date.Should().Be(monday.AddDays(7));
    }

    [Fact]
    public void Short_date_leap_day_rolling_to_non_leap_year_returns_error()
    {
        // Regression: "29.2" on 2025-03-01 — Feb 29 2025 is invalid (non-leap),
        // and the rollover target Feb 29 2026 is also invalid. Must not throw.
        var afterLeap = new DateOnly(2025, 3, 1);
        var p = new DueDateParser(() => afterLeap);
        p.Parse("29.2").IsError.Should().BeTrue();
    }
}
