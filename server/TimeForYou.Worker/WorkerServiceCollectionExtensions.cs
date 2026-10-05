using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TimeForYou.Data;
using TimeForYou.Domain;
using TimeForYou.WebUntis;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Worker;

/// <summary>
/// Registers the whole worker vertical in one line (CONTRIBUTING.md §4).
/// </summary>
public static class WorkerServiceCollectionExtensions
{
    /// <summary>Hangfire's own Postgres schema, kept apart from the app tables in `public` (#14).</summary>
    public const string HangfireSchema = "hangfire";

    /// <param name="scheduleRecurringJob">
    /// The Seam A test-mode switch: <c>false</c> registers <see cref="PollJob"/> and everything it
    /// needs but no Hangfire server or recurring schedule, so a test resolves the job and calls
    /// <see cref="PollJob.RunAsync"/> directly.
    /// </param>
    public static IServiceCollection AddWorkerModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool scheduleRecurringJob = true)
    {
        services.AddDataModule(configuration);
        services.AddWebUntisClient(configuration);
        services.AddWebUntisCredentialProtection(configuration);

        // Real FCM (FirebaseAdmin) replaces this in Week 3; Seam A registers its capturing sender first.
        services.TryAddSingleton<IPushSender, LoggingPushSender>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PollJob>();
        services.AddScoped<IUserPollJob>(provider => provider.GetRequiredService<PollJob>());

        if (!scheduleRecurringJob)
        {
            return services;
        }

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(
                    configuration.GetConnectionString("TimeForYou")
                        ?? throw new InvalidOperationException("ConnectionStrings:TimeForYou is not configured.")),
                new PostgreSqlStorageOptions { SchemaName = HangfireSchema }));
        services.AddHangfireServer();
        services.AddHostedService<PollJobScheduler>();

        return services;
    }
}