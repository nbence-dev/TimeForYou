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
}
