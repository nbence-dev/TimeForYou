using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Api.Contracts;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Api.Features.Timetable;

/// <summary>docs/openapi.yaml `#/components/schemas/Lesson` — one `lesson_snapshots` row.</summary>
public sealed record LessonResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("untis_lesson_id")] string UntisLessonId,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("lecturer")] string? Lecturer,
    [property: JsonPropertyName("room")] string? Room,
    [property: JsonPropertyName("start_ts")] DateTimeOffset StartTs,
    [property: JsonPropertyName("end_ts")] DateTimeOffset EndTs,
    [property: JsonPropertyName("status")] string Status);

/// <summary>docs/openapi.yaml `#/components/schemas/ChangeLogEntry` — one `change_log` row.</summary>
public sealed record ChangeLogEntryResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("lesson_id")] Guid LessonId,
    [property: JsonPropertyName("change_type")] string ChangeType,
    [property: JsonPropertyName("old_value")] string? OldValue,
    [property: JsonPropertyName("new_value")] string? NewValue,
    [property: JsonPropertyName("detected_at")] DateTimeOffset DetectedAt);

/// <summary>GET /timetable and GET /changes — the two read models the Week 3 screens render (#34).</summary>
public static class TimetableEndpoints
{
    /// <summary>`change_log` retention, and so the default and maximum `/changes` window.</summary>
    public static readonly TimeSpan ChangeRetention = TimeSpan.FromDays(30);

    public static IEndpointRouteBuilder MapTimetableEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/timetable", GetTimetableAsync)
            .WithName("GetTimetable")
            .RequireAuthorization();

        endpoints.MapGet("/changes", GetChangesAsync)
            .WithName("GetChanges")
            .RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// "This week and next": Monday 00:00 UTC of the current week up to (excluding) Monday 00:00 UTC
    /// two weeks later. Users carry no timezone, so UTC is the fixed, documented boundary.
    /// </summary>
    internal static (DateTimeOffset From, DateTimeOffset To) LookaheadWindow(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
        var monday = new DateTimeOffset(utc.Date.AddDays(-daysSinceMonday), TimeSpan.Zero);
        return (monday, monday.AddDays(14));
    }

    private static async Task<IResult> GetTimetableAsync(
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        var (from, to) = LookaheadWindow(clock.GetUtcNow());

        var lessons = await db.LessonSnapshots
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.StartTs >= from && l.StartTs < to)
            .OrderBy(l => l.StartTs)
            .ToListAsync(cancellationToken);

        return Results.Ok(lessons.Select(ToResponse));
    }

    private static async Task<IResult> GetChangesAsync(
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        TimeProvider clock,
        string? since,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        var earliest = clock.GetUtcNow() - ChangeRetention;

        var from = earliest;
        if (since is not null)
        {
            if (!DateTimeOffset.TryParse(since, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return Results.BadRequest(new ErrorResponse("since must be an ISO-8601 timestamp."));
            }

            from = parsed > earliest ? parsed : earliest;
        }

        var changes = await db.ChangeLog
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.DetectedAt >= from)
            .OrderByDescending(c => c.DetectedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(changes.Select(ToResponse));
    }

    private static Guid CallerUserId(ClaimsPrincipal caller) =>
        Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);

    private static LessonResponse ToResponse(LessonSnapshot l) => new(
        l.Id, l.UntisLessonId, l.Subject, l.Lecturer, l.Room, l.StartTs, l.EndTs, l.Status.ToWireValue());

    private static ChangeLogEntryResponse ToResponse(ChangeLogEntry c) => new(
        c.Id, c.LessonId, c.ChangeType.ToWireValue(), c.OldValue, c.NewValue, c.DetectedAt);
}
