using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Api.Contracts;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;
using TimeForYou.Data.Entities;

namespace TimeForYou.Api.Features.Notes;

/// <summary>docs/openapi.yaml `#/components/schemas/Note` — one `notes` row.</summary>
public sealed record NoteResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("lesson_id")] Guid LessonId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

/// <summary>docs/openapi.yaml `#/components/schemas/NoteWriteRequest`. Nullable so a missing property is a 400, not a 500.</summary>
public sealed record NoteWriteRequest([property: JsonPropertyName("body")] string? Body);

/// <summary>
/// Notes CRUD (#35). Notes belong to the user, not the device; a lesson or note that isn't the
/// caller's is a 404 so existence is never revealed.
/// </summary>
public static class NoteEndpoints
{
    public const int MaxBodyLength = 500;

    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/lessons/{lessonId:guid}/notes", ListAsync)
            .WithName("ListNotesForLesson")
            .RequireAuthorization();

        endpoints.MapPost("/lessons/{lessonId:guid}/notes", CreateAsync)
            .WithName("CreateNote")
            .RequireAuthorization();

        endpoints.MapPatch("/notes/{noteId:guid}", UpdateAsync)
            .WithName("UpdateNote")
            .RequireAuthorization();

        endpoints.MapDelete("/notes/{noteId:guid}", DeleteAsync)
            .WithName("DeleteNote")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid lessonId,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        if (!await OwnsLessonAsync(db, userId, lessonId, cancellationToken))
        {
            return LessonNotFound();
        }

        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.LessonId == lessonId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(notes.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        Guid lessonId,
        NoteWriteRequest? request,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        if (!await OwnsLessonAsync(db, userId, lessonId, cancellationToken))
        {
            return LessonNotFound();
        }

        if (Validate(request) is { } error)
        {
            return error;
        }

        var now = clock.GetUtcNow();
        var note = new Note
        {
            UserId = userId,
            LessonId = lessonId,
            Body = request!.Body!,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/notes/{note.Id}", ToResponse(note));
    }

    private static async Task<IResult> UpdateAsync(
        Guid noteId,
        NoteWriteRequest? request,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        var note = await db.Notes.SingleOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, cancellationToken);
        if (note is null)
        {
            return NoteNotFound();
        }

        if (Validate(request) is { } error)
        {
            return error;
        }

        note.Body = request!.Body!;
        note.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(note));
    }

    private static async Task<IResult> DeleteAsync(
        Guid noteId,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        var note = await db.Notes.SingleOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, cancellationToken);
        if (note is null)
        {
            return NoteNotFound();
        }

        db.Notes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IResult? Validate(NoteWriteRequest? request) => request?.Body switch
    {
        null => Results.BadRequest(new ErrorResponse("body is required.")),
        var body when string.IsNullOrWhiteSpace(body) => Results.BadRequest(new ErrorResponse("body must not be empty.")),
        var body when body.Length > MaxBodyLength =>
            Results.BadRequest(new ErrorResponse($"body must be at most {MaxBodyLength} characters.")),
        _ => null,
    };

    private static Task<bool> OwnsLessonAsync(
        TimeForYouDbContext db, Guid userId, Guid lessonId, CancellationToken cancellationToken) =>
        db.LessonSnapshots.AnyAsync(l => l.Id == lessonId && l.UserId == userId, cancellationToken);

    private static IResult LessonNotFound() => Results.NotFound(new ErrorResponse("Lesson not found."));

    private static IResult NoteNotFound() => Results.NotFound(new ErrorResponse("Note not found."));

    private static Guid CallerUserId(ClaimsPrincipal caller) =>
        Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);

    private static NoteResponse ToResponse(Note n) => new(n.Id, n.LessonId, n.Body, n.CreatedAt, n.UpdatedAt);
}
