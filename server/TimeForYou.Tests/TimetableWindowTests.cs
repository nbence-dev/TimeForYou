using TimeForYou.Domain;

namespace TimeForYou.Tests;

/// <summary>Seam B (#3): the shared window the poll fetches and GET /timetable serves (#59). No I/O.</summary>
public sealed class TimetableWindowTests
{
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    [Fact]
    public void Window_runs_from_this_weeks_monday_through_the_sunday_seven_weeks_later()
    {
        // Wednesday 2026-10-07: this week's Monday is 2026-10-05; seven weeks on, Sunday is 2026-11-22.
        var window = TimetableWindow.Containing(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 5), window.FirstDay);
        Assert.Equal(new DateOnly(2026, 11, 22), window.LastDay);
    }

    [Fact]
    public void Window_is_48_days_past_the_first_monday_inclusive()
    {
        var window = TimetableWindow.Containing(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(DayOfWeek.Monday, window.FirstDay.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, window.LastDay.DayOfWeek);
        Assert.Equal(48, window.LastDay.DayNumber - window.FirstDay.DayNumber);
    }

    [Fact]
    public void Window_bounds_are_midnight_south_african_time()
    {
        var window = TimetableWindow.Containing(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 0, 0, 0, Sast), window.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 0, 0, 0, Sast), window.EndsBefore);
        Assert.Equal(TimeSpan.FromDays(49), window.EndsBefore - window.StartsAt);
    }

    [Fact]
    public void Monday_just_after_midnight_in_south_africa_already_belongs_to_the_new_week()
    {
        // Sunday 22:30 UTC is Monday 00:30 SAST: the window must have rolled forward.
        var window = TimetableWindow.Containing(new DateTimeOffset(2026, 10, 4, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 5), window.FirstDay);
    }

    [Fact]
    public void Sunday_just_before_midnight_in_south_africa_still_belongs_to_the_old_week()
    {
        // Sunday 21:59 UTC is Sunday 23:59 SAST: still last week.
        var window = TimetableWindow.Containing(new DateTimeOffset(2026, 10, 4, 21, 59, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 9, 28), window.FirstDay);
    }

    [Fact]
    public void Window_gives_the_same_answer_whatever_offset_the_clock_reading_carries()
    {
        var utc = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var elsewhere = utc.ToOffset(TimeSpan.FromHours(-8));

        Assert.Equal(TimetableWindow.Containing(utc), TimetableWindow.Containing(elsewhere));
    }
}
