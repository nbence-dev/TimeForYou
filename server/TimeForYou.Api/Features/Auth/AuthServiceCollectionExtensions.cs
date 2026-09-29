using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace TimeForYou.Api.Features.Auth;

/// <summary>
/// Lets api register the whole auth vertical in one line (CONTRIBUTING.md §4).
/// </summary>
public static class AuthServiceCollectionExtensions
{
    public const string BearerScheme = "DeviceBearer";

    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection()
            .SetApplicationName("TimeForYou")
            .DisableAutomaticKeyGeneration();

        // Read lazily (when KeyManagementOptions is first bound), not here: `configuration` is
        // usually the WebApplicationBuilder's ConfigurationManager, which can still gain sources
        // — e.g. a WebApplicationFactory-based test's overrides — between this call and
        // `builder.Build()` (issue #31).
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new FixedKeyXmlRepository(ResolveMasterKey(configuration)));

        services.AddAuthentication(BearerScheme)
            .AddScheme<AuthenticationSchemeOptions, DeviceBearerAuthenticationHandler>(BearerScheme, _ => { });
        services.AddAuthorization();

        return services;
    }

    private static byte[] ResolveMasterKey(IConfiguration configuration)
    {
        var aesKey = configuration["AES_KEY"]
            ?? throw new InvalidOperationException("AES_KEY is not configured.");
        try
        {
            return Convert.FromBase64String(aesKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("AES_KEY must be base64 (see .env.example: openssl rand -base64 32).", exception);
        }
    }
}
