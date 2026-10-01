using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TimeForYou.WebUntis;

/// <summary>
/// Lets api and worker each register this layer in one line (CONTRIBUTING.md §4 — Program.cs is a
/// collision magnet across six people, so every vertical registers itself through a single call).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebUntisClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Read lazily (per typed-client creation), not here: `configuration` is usually the
        // WebApplicationBuilder's ConfigurationManager, which can still gain sources — e.g. a
        // WebApplicationFactory-based test's overrides — between this call and `builder.Build()`
        // (issue #31).
        services.AddHttpClient<IWebUntisClient, WebUntisClient>(client =>
        {
            var baseUrl = configuration["WEBUNTIS_BASE_URL"]
                ?? throw new InvalidOperationException("WEBUNTIS_BASE_URL is not configured.");
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }

    /// <summary>
    /// DataProtection keyed from <c>AES_KEY</c> plus <see cref="WebUntisCredentialProtector"/>, so the
    /// api (#31) and worker (#32) encrypt and decrypt credentials identically.
    /// </summary>
    public static IServiceCollection AddWebUntisCredentialProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection()
            .SetApplicationName("TimeForYou")
            .DisableAutomaticKeyGeneration();

        // Read lazily (when KeyManagementOptions is first bound), for the same reason as above.
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new FixedKeyXmlRepository(ResolveMasterKey(configuration)));

        services.AddSingleton<WebUntisCredentialProtector>();

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