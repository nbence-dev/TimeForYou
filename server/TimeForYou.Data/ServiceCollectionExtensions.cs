using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TimeForYou.Data;

/// <summary>
/// Lets api and worker each register this layer in one line (CONTRIBUTING.md §4 — Program.cs is a
/// collision magnet across six people, so every vertical registers itself through a single call).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataModule(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<TimeForYouDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("TimeForYou")));
}
