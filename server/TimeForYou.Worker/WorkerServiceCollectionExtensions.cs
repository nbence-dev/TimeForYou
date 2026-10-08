using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
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

        // Seam A registers its capturing sender first, so TryAdd leaves it in place.
        AddPushSender(services, configuration);
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
        // Registered before the Hangfire server so hosted services start in order: the worker
        // waits for the API's migrations rather than polling against missing tables (#62).
        services.AddHostedService<SchemaReadyGate>();
        services.AddHangfireServer();
        services.AddHostedService<PollJobScheduler>();

        return services;
    }

    /// <summary>
    /// FCM when a Firebase service account is configured and present on disk; otherwise the logging
    /// sender, so the worker still starts with no credentials (#95).
    /// </summary>
    private static void AddPushSender(IServiceCollection services, IConfiguration configuration)
    {
        var credentialsPath = configuration["GOOGLE_APPLICATION_CREDENTIALS"];
        if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
        {
            services.TryAddSingleton<IPushSender, LoggingPushSender>();
            services.AddSingleton<IHostedService>(provider => new PushFallbackWarning(
                provider.GetRequiredService<ILogger<PushFallbackWarning>>(), credentialsPath));
            return;
        }

        services.TryAddSingleton(_ => FirebaseMessaging.GetMessaging(
            FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialsPath),
                ProjectId = configuration["FIREBASE_PROJECT_ID"],
            })));
        services.TryAddSingleton<IPushSender>(provider => new FcmPushSender(
            provider.GetRequiredService<FirebaseMessaging>(),
            provider.GetRequiredService<ILogger<FcmPushSender>>(),
            configuration["WEB_APP_URL"]));
    }
}