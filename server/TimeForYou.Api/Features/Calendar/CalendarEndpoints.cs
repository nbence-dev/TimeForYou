using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeForYou.Api.Contracts;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Api.Features.Calendar;

/// <summary>docs/openapi.yaml `#/components/schemas/CalendarFeed` — where the caller's feed lives.</summary>
public sealed record CalendarFeedResponse([property: JsonPropertyName("url")] string Url);

/// <summary>GET /calendar and POST /calendar/reset (bearer), and GET /ics/{token}.ics (opaque token) — the iCalendar feed (#37, #112).</summary>
public static class CalendarEndpoints
{
    /// <summary>Optional config key: the externally reachable origin of the API, e.g. the tunnel's
    /// https URL. When unset, `GET /calendar` falls back to the origin the request arrived on.</summary>
    public const string PublicBaseUrlKey = "PUBLIC_BASE_URL";

    private const int TokenLength = 64;

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/calendar", GetCalendarAsync)
            .WithName("GetCalendar")
            .RequireAuthorization();

        endpoints.MapPost("/calendar/reset", ResetCalendarAsync)
            .WithName("ResetCalendar")
            .RequireAuthorization();

        endpoints.MapGet("/ics/{token}.ics", GetIcsFeedAsync)
            .WithName("GetIcsFeed")
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// 48 random bytes, base64url-encoded: exactly 64 URL-safe characters carrying 384 bits of
    /// entropy (NFR-6 asks for at least 256), and no padding to trip up a path segment.
    /// </summary>
    internal static string GenerateIcsToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_');

    private static async Task<IResult> GetCalendarAsync(
        ClaimsPrincipal caller,
        HttpRequest request,
        IConfiguration configuration,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);

        var token = await db.IcsTokens
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .SingleOrDefaultAsync(cancellationToken);

        if (token is null)
        {
            db.IcsTokens.Add(new IcsToken { Token = GenerateIcsToken(), UserId = userId, CreatedAt = clock.GetUtcNow() });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUserIdConflict(exception))
            {
                // Two of the user's devices asked at the same instant and both found no row; the
                // other request's token won. Hand back that one so every device sees the same URL.
                db.ChangeTracker.Clear();
            }

            token = await db.IcsTokens
                .Where(t => t.UserId == userId)
                .Select(t => t.Token)
                .SingleAsync(cancellationToken);
        }

        return Results.Ok(FeedFor(token, request, configuration));
    }

    /// <summary>
    /// POST /calendar/reset (#112): swaps the caller's feed token for a new one, so the old URL stops
    /// working (404) and every device of the user gets the new one from `GET /calendar`. The token is
    /// the table's primary key, so this is a plain SQL update, not an EF change on a tracked key.
    /// </summary>
    private static async Task<IResult> ResetCalendarAsync(
        ClaimsPrincipal caller,
        HttpRequest request,
        IConfiguration configuration,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);
        var token = GenerateIcsToken();
        var now = clock.GetUtcNow();

        var replaced = await db.IcsTokens
            .Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Token, token).SetProperty(t => t.CreatedAt, now),
                cancellationToken);

        if (replaced == 0)
        {
            // The student never opened the feed, so there is nothing to replace yet.
            db.IcsTokens.Add(new IcsToken { Token = token, UserId = userId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(FeedFor(token, request, configuration));
    }

    private static CalendarFeedResponse FeedFor(string token, HttpRequest request, IConfiguration configuration)
    {
        var configured = configuration[PublicBaseUrlKey];
        var origin = string.IsNullOrWhiteSpace(configured)
            ? $"{request.Scheme}://{request.Host}"
            : configured.TrimEnd('/');
        return new CalendarFeedResponse($"{origin}/ics/{token}.ics");
    }

    private static async Task<IResult> GetIcsFeedAsync(
        string token,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = token.Length == TokenLength
            ? await db.IcsTokens
                .Where(t => t.Token == token)
                .Select(t => (Guid?)t.UserId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        if (userId is null)
        {
            return Results.NotFound(new ErrorResponse("No calendar feed matches this token."));
        }

        var now = clock.GetUtcNow();
        var lessons = await db.LessonSnapshots
            .AsNoTracking()
            .Include(l => l.Notes)
            .Where(l => l.UserId == userId && l.StartTs > now)
            .OrderBy(l => l.StartTs)
            .ToListAsync(cancellationToken);

        var calendar = new Ical.Net.Calendar();
        calendar.AddProperty("X-WR-CALNAME", "TimeForYou timetable");
        foreach (var lesson in lessons)
        {
            calendar.Events.Add(ToEvent(lesson));
        }

        var ics = new CalendarSerializer().SerializeToString(calendar);
        return Results.Text(ics, "text/calendar", System.Text.Encoding.UTF8);
    }

    private static CalendarEvent ToEvent(LessonSnapshot lesson)
    {
        // A note edit changes the event's DESCRIPTION without touching the lesson row, so the
        // modified stamp must follow the newest of the two or clients keep their cached text.
        var modified = lesson.Notes.Select(n => n.UpdatedAt).Append(lesson.UpdatedAt).Max();

        return new CalendarEvent
        {
            // Stable per upstream lesson, so a calendar app updates the event in place after a room
            // change instead of adding a second one (FR-41).
            Uid = $"{lesson.UntisLessonId}@timeforyou",
            Summary = lesson.Subject,
            Location = lesson.Room,
            Description = Describe(lesson),
            DtStart = ToCalDateTime(lesson.StartTs),
            DtEnd = ToCalDateTime(lesson.EndTs),
            DtStamp = ToCalDateTime(modified),
            LastModified = ToCalDateTime(modified),
            // Cancelled lessons stay in the feed, struck through, rather than vanishing (FR-42).
            Status = lesson.Status == LessonStatus.Cancelled ? EventStatus.Cancelled : EventStatus.Confirmed,
        };
    }

    private static string? Describe(LessonSnapshot lesson)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(lesson.Lecturer))
        {
            lines.Add($"Lecturer: {lesson.Lecturer}");
        }

        lines.AddRange(lesson.Notes.OrderBy(n => n.CreatedAt).Select(n => n.Body));
        return lines.Count == 0 ? null : string.Join("\n\n", lines);
    }

    private static CalDateTime ToCalDateTime(DateTimeOffset value) => new(value.UtcDateTime, "UTC");

    private static bool IsUserIdConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
