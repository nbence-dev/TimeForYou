using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data;

namespace TimeForYou.Api.Features.Profile;

/// <summary>docs/openapi.yaml `Profile`.</summary>
public sealed record ProfileResponse(
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("class_name")] string? ClassName);

/// <summary>
/// docs/openapi.yaml `GET /profile` (#113): the caller's name and class as WebUntis shows them. The
/// worker saves them on each poll, so both are null until the first poll has run. Read-only: the
/// profile comes from WebUntis and is not edited here.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/profile", GetProfileAsync)
            .WithName("GetProfile")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal caller,
        TimeForYouDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(caller.FindFirstValue(DeviceBearerAuthenticationHandler.UserIdClaimType)!);
        var profile = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new ProfileResponse(u.DisplayName, u.ClassName))
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }
}