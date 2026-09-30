using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeForYou.Api.Contracts;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.WebUntis;

namespace TimeForYou.Api.Features.Auth;

/// <summary>docs/openapi.yaml `#/paths/qr-scan` — the door every other API ticket walks through (#31).</summary>
public sealed record QrScanRequest(string? Server, string? School, string? User, string? Key);

public sealed record QrScanResponse(string Token, Guid UserId);

public static class AuthEndpoints
{
    /// <summary>The DataProtection purpose string scoping the credential protector — changing this
    /// would make every existing `credentials_protected` value undecryptable.</summary>
    private const string CredentialProtectionPurpose = "TimeForYou.WebUntisCredentials.v1";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/qr-scan", ScanQrCodeAsync)
            .WithName("ScanQrCode")
            .AllowAnonymous();

        // A protected dummy endpoint proving the bearer handler actually gates access (#31 AC),
        // until #34 lands its own. Not part of docs/openapi.yaml.
        endpoints.MapGet("/auth/session", GetSession)
            .WithName("GetAuthSession")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ScanQrCodeAsync(
        QrScanRequest request,
        TimeForYouDbContext db,
        IWebUntisClient webUntisClient,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Server) ||
            string.IsNullOrWhiteSpace(request.School) ||
            string.IsNullOrWhiteSpace(request.User) ||
            string.IsNullOrWhiteSpace(request.Key))
        {
            return Results.BadRequest(new ErrorResponse("server, school, user and key are all required."));
        }

        var credentials = new WebUntisCredentials(request.Server, request.School, request.User, request.Key);
        try
        {
            await webUntisClient.LoginAsync(credentials, cancellationToken);
        }
        catch (WebUntisCredentialsRejectedException)
        {
            return Results.BadRequest(new ErrorResponse("WebUntis rejected the supplied credentials."));
        }

        var untisUserHash = AuthCrypto.HashUntisUser(request.School, request.User);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UntisUserHash == untisUserHash, cancellationToken);
        var isNewUser = user is null;

        if (user is null)
        {
            var protector = dataProtectionProvider.CreateProtector(CredentialProtectionPurpose);
            var protectedCredentials = protector.Protect(JsonSerializer.Serialize(credentials));

            user = new User
            {
                School = request.School,
                CredentialsProtected = protectedCredentials,
                UntisUserHash = untisUserHash,
            };
            db.Users.Add(user);
        }

        var token = AuthCrypto.GenerateBearerToken();
        var device = new Device { User = user, BearerTokenHash = AuthCrypto.HashBearerToken(token) };
        db.Devices.Add(device);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (isNewUser && IsUntisUserHashConflict(exception))
        {
            // Two devices scanned the same QR code at the same instant and both lost the
            // find-or-create race (#31 — "same user" is only guaranteed for scans that don't
            // literally overlap). The other request's user row won; adopt it instead of failing.
            db.Entry(user).State = EntityState.Detached;
            db.Entry(device).State = EntityState.Detached;

            user = await db.Users.SingleAsync(u => u.UntisUserHash == untisUserHash, cancellationToken);
            device = new Device { User = user, BearerTokenHash = AuthCrypto.HashBearerToken(token) };
            db.Devices.Add(device);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new QrScanResponse(token, user.Id));
    }

    private static bool IsUntisUserHashConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_untis_user_hash" };

    private static IResult GetSession(ClaimsPrincipal caller)
    {
        var userId = caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType);
        var deviceId = caller.FindFirstValue(DeviceBearerAuthenticationHandler.DeviceIdClaimType);
        return Results.Ok(new { user_id = userId, device_id = deviceId });
    }
}
