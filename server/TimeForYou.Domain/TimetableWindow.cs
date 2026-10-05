namespace TimeForYou.Domain;

/// <summary>
/// The one definition of how far ahead the system looks (#59): the worker fetches exactly this
/// window and GET /timetable serves exactly this window, so they cannot drift apart.
/// </summary>
/// <remarks>
/// Seven weeks, because the cohort works in 7-week blocks before a recess (#3, user story 7): the
/// Monday of the current week through the Sunday seven weeks later, 49 days in all. It rolls
/// forward every Monday and is not aligned to term or recess dates.
/// Day boundaries are South African time (UTC+2, no DST): Eduvos is the source of the timetable and
/// the mock uses the same offset, so everyone agrees which Monday a lesson belongs to.
/// </remarks>
public readonly record struct TimetableWindow(DateOnly FirstDay, DateOnly LastDay)
{
    public const int Weeks = 7;

    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>The window that contains the given moment.</summary>
    public static TimetableWindow Containing(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(Offset).Date);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return new TimetableWindow(monday, monday.AddDays(Weeks * 7 - 1));
    }

    // The bounds are returned as UTC: Postgres timestamptz only accepts UTC DateTimeOffsets as query
    // parameters, and this is the value both the poll and GET /timetable compare against.

    /// <summary>South African midnight at the start of <see cref="FirstDay"/>: a lesson starting here is inside.</summary>
    public DateTimeOffset StartsAt => new DateTimeOffset(FirstDay.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

    /// <summary>South African midnight after <see cref="LastDay"/>: a lesson starting here is outside.</summary>
    public DateTimeOffset EndsBefore => new DateTimeOffset(LastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
}
