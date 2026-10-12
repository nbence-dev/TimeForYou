using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Api.Contracts;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;

namespace TimeForYou.Api.Features.Settings;

/// <summary>docs/openapi.yaml `UserSettings`.</summary>
public sealed record UserSettingsResponse(
    [property: JsonPropertyName("change_alerts_enabled")] bool ChangeAlertsEnabled);

/// <summary>docs/openapi.yaml `UserSettingsUpdate`.</summary>
public sealed record UserSettingsUpdateRequest(
    [property: JsonPropertyName("change_alerts_enabled")] bool? ChangeAlertsEnabled);

/// <summary>
/// docs/openapi.yaml `GET /settings` and `PATCH /settings` (#110): the caller's per-user settings.
/// They live on the `users` row, not the device, so every device the student has linked sees the
/// same values.
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/settings", GetSettingsAsync)
            .WithName("GetSettings")
            .RequireAuthorization();

        endpoints.MapPatch("/settings", UpdateSettingsAsync)
            .WithName("UpdateSettings")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetSettingsAsync(
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = CallerUserId(caller);
        var enabled = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.ChangeAlertsEnabled)
            .SingleOrDefaultAsync(cancellationToken);

        return enabled is { } value
            ? Results.Ok(new UserSettingsResponse(value))
            : Results.Unauthorized();
    }

    private static async Task<IResult> UpdateSettingsAsync(
        UserSettingsUpdateRequest request,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.ChangeAlertsEnabled is not { } enabled)
        {
            return Results.BadRequest(new ErrorResponse("change_alerts_enabled is required."));
        }

        var userId = CallerUserId(caller);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        user.ChangeAlertsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserSettingsResponse(user.ChangeAlertsEnabled));
    }

    private static Guid CallerUserId(ClaimsPrincipal caller) =>
        Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);
}