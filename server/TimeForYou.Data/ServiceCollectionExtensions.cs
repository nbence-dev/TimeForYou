using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Applies any pending EF Core migrations (#62). Idempotent: with nothing pending it changes
    /// nothing. Throws if a migration fails, so the caller's host never starts serving against a
    /// half-migrated schema. Only the API calls this — the worker waits via
    /// <see cref="WaitForSchemaAsync"/> instead of racing it.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TimeForYouDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Blocks until no migrations are pending, retrying every <paramref name="retryDelay"/> while
    /// the API is still migrating (or Postgres is still accepting connections). Never applies anything.
    /// </summary>
    public static async Task WaitForSchemaAsync(
        this IServiceProvider services,
        TimeSpan retryDelay,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<TimeForYouDbContext>();
                if (!(await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    return;
                }

                logger.LogInformation("Waiting for the API to finish applying migrations.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Database not ready yet; retrying.");
            }

            await Task.Delay(retryDelay, cancellationToken);
        }
    }
}
