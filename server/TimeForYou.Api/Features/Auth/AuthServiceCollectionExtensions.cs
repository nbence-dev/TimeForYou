using Microsoft.AspNetCore.Authentication;
using TimeForYou.WebUntis;

namespace TimeForYou.Api.Features.Auth;

/// <summary>
/// Lets api register the whole auth vertical in one line (CONTRIBUTING.md §4).
/// </summary>
public static class AuthServiceCollectionExtensions
{
    public const string BearerScheme = "DeviceBearer";

    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Credential encryption lives in TimeForYou.WebUntis so the worker (#32) decrypts with the
        // exact same AES_KEY-derived key and purpose string the api encrypts with.
        services.AddWebUntisCredentialProtection(configuration);

        services.AddAuthentication(BearerScheme)
            .AddScheme<AuthenticationSchemeOptions, DeviceBearerAuthenticationHandler>(BearerScheme, _ => { });
        services.AddAuthorization();

        return services;
    }
}