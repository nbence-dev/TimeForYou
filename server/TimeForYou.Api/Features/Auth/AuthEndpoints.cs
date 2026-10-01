using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeForYou.Api.Contracts;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.WebUntis;

namespace TimeForYou.Api.Features.Auth;

/// <summary>docs/openapi.yaml `#/paths/qr-scan` — the door every other API ticket walks through (#31).</summary>
public sealed record QrScanRequest(string? Server, string? School, string? User, string? Key);

public sealed record QrScanResponse(string Token, [property: JsonPropertyName("user_id")] Guid UserId);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/qr-scan", ScanQrCodeAsync)
            .WithName("ScanQrCode")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ScanQrCodeAsync(
        QrScanRequest request,
        TimeForYouDbContext db,
        IWebUntisClient webUntisClient,
        WebUntisCredentialProtector credentialProtector,
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
            user = new User
            {
                School = request.School,
                CredentialsProtected = credentialProtector.Protect(credentials),
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
}
