using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Api.Contracts;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;
using TimeForYou.Domain;

namespace TimeForYou.Api.Features.Devices;

/// <summary>docs/openapi.yaml `DeviceRegisterRequest`.</summary>
public sealed record DeviceRegisterRequest(
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("push_token")] string? PushToken);

/// <summary>docs/openapi.yaml `Device`.</summary>
public sealed record DeviceResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("push_token")] string PushToken,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

/// <summary>
/// docs/openapi.yaml `POST /devices` (#36): stores the FCM registration token on the calling
/// device's own row — the one its bearer token belongs to. Calling again overwrites the token;
/// it never creates a second row, and a token already held by another row is moved here.
/// </summary>
public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/devices", RegisterDeviceAsync)
            .WithName("RegisterDevice")
            .RequireAuthorization();

        endpoints.MapDelete("/devices", UnlinkDeviceAsync)
            .WithName("UnlinkDevice")
            .RequireAuthorization();

        return endpoints;
    }

    /// <summary>Deletes only the calling device's row; the user, stored login and notes stay (#107).</summary>
    private static async Task<IResult> UnlinkDeviceAsync(
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        var deviceId = Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.DeviceIdClaimType)!);
        await db.Devices.Where(d => d.Id == deviceId).ExecuteDeleteAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RegisterDeviceAsync(
        DeviceRegisterRequest request,
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PushToken))
        {
            return Results.BadRequest(new ErrorResponse("push_token is required."));
        }

        if (!TryParsePlatform(request.Platform, out var platform))
        {
            return Results.BadRequest(new ErrorResponse("platform must be one of: android_fcm, web_push."));
        }

        var deviceId = Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.DeviceIdClaimType)!);
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return Results.Unauthorized();
        }

        // A re-scan on a reinstalled app leaves an older row holding the same FCM token;
        // clear it there so the phone is pushed to once, not once per row.
        await db.Devices
            .Where(d => d.Id != deviceId && d.PushToken == request.PushToken)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.PushToken, (string?)null), cancellationToken);

        device.Platform = platform;
        device.PushToken = request.PushToken;
        device.LastSeenAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new DeviceResponse(device.Id, platform.ToWireValue(), request.PushToken, device.CreatedAt));
    }

    private static bool TryParsePlatform(string? value, out DevicePlatform platform)
    {
        try
        {
            platform = DevicePlatformExtensions.ParseWireValue(value ?? string.Empty);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            platform = default;
            return false;
        }
    }
}