using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TimeForYou.Api.Contracts;
using TimeForYou.Data;

namespace TimeForYou.Api.Features.Auth;

/// <summary>
/// Looks up the bearer token's hash against `devices.bearer_token_hash`, sets the caller's
/// `user_id`/`device_id` claims, and bumps `last_seen_at` (issue #31). Missing or unknown tokens
/// fail authentication; ASP.NET Core's default challenge then calls <see cref="HandleChallengeAsync"/>.
/// </summary>
public sealed class DeviceBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeForYouDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string UserIdClaimType = "user_id";
    public const string DeviceIdClaimType = "device_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues) ||
            !AuthenticationHeaderValue.TryParse(headerValues.ToString(), out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter))
        {
            return AuthenticateResult.Fail("Missing bearer token.");
        }

        var tokenHash = AuthCrypto.HashBearerToken(header.Parameter);
        var device = await db.Devices.FirstOrDefaultAsync(d => d.BearerTokenHash == tokenHash);
        if (device is null)
        {
            return AuthenticateResult.Fail("Unknown bearer token.");
        }

        device.LastSeenAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(
            [
                new Claim(UserIdClaimType, device.UserId.ToString()),
                new Claim(DeviceIdClaimType, device.Id.ToString()),
            ],
            Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new ErrorResponse("Missing, unknown, or expired bearer token."));
    }
}
