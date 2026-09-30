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
        var baseUrl = configuration["WEBUNTIS_BASE_URL"]
            ?? throw new InvalidOperationException("WEBUNTIS_BASE_URL is not configured.");

        services.AddHttpClient<IWebUntisClient, WebUntisClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
