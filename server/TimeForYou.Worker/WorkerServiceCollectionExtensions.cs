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
    /// sender, so the worker still starts with no credentials (#95). A configuration problem is
    /// logged once at startup: as an error when something was set but is wrong, as a warning when
    /// it was simply left unset.
    /// </summary>
    private static void AddPushSender(IServiceCollection services, IConfiguration configuration)
    {
        // A test host (Seam A) may already have supplied its own sender: leave it, and stay quiet.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IPushSender))) return;

        var credentialsPath = configuration["GOOGLE_APPLICATION_CREDENTIALS"];
        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            AddLoggingFallback(services, LogLevel.Warning,
                "GOOGLE_APPLICATION_CREDENTIALS is unset; push notifications will be logged, not sent");
            return;
        }

        if (!File.Exists(credentialsPath))
        {
            AddLoggingFallback(services, LogLevel.Error,
                $"GOOGLE_APPLICATION_CREDENTIALS ({credentialsPath}) is not a file; push notifications will be logged, not sent");
            return;
        }

        var webAppUrl = ParseWebAppUrl(configuration["WEB_APP_URL"], out var webAppUrlProblem);
        if (webAppUrlProblem is not null) AddStartupNotice(services, LogLevel.Warning, webAppUrlProblem);

        // The service-account file already names its project, so only an explicit, real id overrides
        // it. A blank or the .env.example placeholder would otherwise send every push to the wrong place.
        var configuredProjectId = configuration["FIREBASE_PROJECT_ID"];
        var projectId = IsRealValue(configuredProjectId) ? configuredProjectId : null;

        services.TryAddSingleton(_ => FirebaseMessaging.GetMessaging(
            FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialsPath),
                ProjectId = projectId,
            })));
        services.TryAddSingleton<IPushSender>(provider => new FcmPushSender(
            provider.GetRequiredService<FirebaseMessaging>(),
            provider.GetRequiredService<ILogger<FcmPushSender>>(),
            webAppUrl));
    }

    /// <summary>
    /// The web app's origin for the "tap opens Changes" link, or <c>null</c> for no link. Firebase
    /// rejects any link that is not an absolute https URL, which would make every web push fail, so
    /// anything else is dropped and <paramref name="problem"/> says why. Blank is fine: no link.
    /// </summary>
    internal static string? ParseWebAppUrl(string? value, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            return trimmed.TrimEnd('/');
        }

        problem = $"WEB_APP_URL ({value}) is not an absolute https URL, which Firebase requires; web pushes will be sent without a link to the Changes screen";
        return null;
    }

    private static bool IsRealValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith("CHANGEME", StringComparison.Ordinal);

    private static void AddLoggingFallback(IServiceCollection services, LogLevel level, string message)
    {
        services.TryAddSingleton<IPushSender, LoggingPushSender>();
        AddStartupNotice(services, level, message);
    }

    private static void AddStartupNotice(IServiceCollection services, LogLevel level, string message) =>
        services.AddSingleton<IHostedService>(provider => new PushStartupNotice(
            provider.GetRequiredService<ILogger<PushStartupNotice>>(), level, message));
}