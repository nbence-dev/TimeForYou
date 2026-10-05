using Hangfire;
using Hangfire.PostgreSql;
using TimeForYou.Domain;

namespace TimeForYou.Api.Features.Auth;

/// <summary>Hands a newly created user's first poll to the worker (#58).</summary>
public interface IFirstPollEnqueuer
{
    void Enqueue(Guid userId);
}

/// <summary>
/// Writes <see cref="IUserPollJob.RunForUserAsync"/> into the Hangfire storage the worker already
/// processes (the `hangfire` schema in the shared Postgres). The api only enqueues; it runs no
/// Hangfire server. Chosen over calling the worker on the LAN: no new env var or URL, and the job
/// survives the worker being down — it runs once the worker is back.
/// </summary>
public sealed class HangfireFirstPollEnqueuer(IBackgroundJobClient jobs) : IFirstPollEnqueuer
{
    public void Enqueue(Guid userId) =>
        jobs.Enqueue<IUserPollJob>(job => job.RunForUserAsync(userId, CancellationToken.None));
}

public static class FirstPollServiceCollectionExtensions
{
    /// <summary>Must match the worker's Hangfire storage settings (WorkerServiceCollectionExtensions),
    /// or the worker can't read the jobs the api writes.</summary>
    private const string HangfireSchema = "hangfire";

    public static IServiceCollection AddFirstPollEnqueuer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(
                    configuration.GetConnectionString("TimeForYou")
                        ?? throw new InvalidOperationException("ConnectionStrings:TimeForYou is not configured.")),
                new PostgreSqlStorageOptions { SchemaName = HangfireSchema }));

        services.AddSingleton<IFirstPollEnqueuer, HangfireFirstPollEnqueuer>();

        return services;
    }
}
