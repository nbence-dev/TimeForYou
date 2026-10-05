using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using TimeForYou.Data;
using TimeForYou.Domain;
using TimeForYou.Worker;

namespace TimeForYou.Tests.Infrastructure;

/// <summary>
/// The Seam A harness (issue #27): starts Postgres 16 and the mock — built from
/// <c>mock/Dockerfile</c>, exactly what ships — once per test run, and hands out everything a
/// test needs to drive the system through its real HTTP boundary: a migrated database, the
/// mock's base URL, an in-process API via <see cref="WebApplicationFactory{TEntryPoint}"/>, and a
/// worker service provider built against the same containers.
///
/// #28 (the mock's own admin reset endpoint) hasn't landed yet, so <see cref="ResetDatabaseAsync"/>
/// only clears this side's Postgres tables. Until #28 lands, tests that mutate mock state must not
/// run interleaved with each other — sharing this fixture via <see cref="SeamACollection"/> is what
/// guarantees that, since xUnit never parallelizes classes within one collection.
/// </summary>
public sealed class SeamAFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private IContainer _mock = null!;
    private IFutureDockerImage _mockImage = null!;

    public string MockBaseUrl { get; private set; } = null!;

    public CapturingPushSender PushSender { get; } = new();

    public async Task InitializeAsync()
    {
        _mockImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(RepoPaths.MockContextDirectory)
            .WithDockerfile("Dockerfile")
            .WithDeleteIfExists(true)
            .Build();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _mockImage.CreateAsync());

        _mock = new ContainerBuilder(_mockImage)
            .WithPortBinding(8000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/qr-payload").ForPort(8000)))
            .Build();
        await _mock.StartAsync();
        MockBaseUrl = $"http://{_mock.Hostname}:{_mock.GetMappedPublicPort(8000)}";

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _mock.DisposeAsync();
        await _mockImage.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public TimeForYouDbContext CreateDbContext() => CreateDbContext(_postgres.GetConnectionString());

    public static TimeForYouDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TimeForYouDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TimeForYouDbContext(options);
    }

    /// <summary>
    /// Creates a brand-new, empty database on the shared Postgres (no migrations applied) and
    /// returns its connection string — for tests that prove the API migrates on startup.
    /// </summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        await using var admin = CreateDbContext();
        var createSql = "create database \"" + name + "\"";
        await admin.Database.ExecuteSqlRawAsync(createSql);
        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = name }.ConnectionString;
    }

    /// <summary>Fixed test-only key (32 zero bytes, base64) — not a secret, never used outside
    /// this harness. Every test run derives the same DataProtection key from it (#31), which is
    /// the point: no key ring to share or reset between tests.</summary>
    private const string TestAesKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>Boots TimeForYou.Api in-process, pointed at this fixture's Postgres and mock.</summary>
    public WebApplicationFactory<Program> CreateApiFactory(string? connectionString = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TimeForYou"] = connectionString ?? _postgres.GetConnectionString(),
                    ["WEBUNTIS_BASE_URL"] = MockBaseUrl,
                    ["AES_KEY"] = TestAesKey,
                })));

    /// <summary>
    /// Builds the worker's DI container against this fixture's Postgres and mock in test mode
    /// (#32): the poll job and everything it needs are registered, with <see cref="PushSender"/>
    /// in place of the real sender, but no Hangfire server or recurring schedule — a test resolves
    /// <see cref="Worker.Polling.PollJob"/> and invokes it directly. Uses the same AES_KEY as the
    /// API, so the worker can decrypt credentials the API encrypted.
    /// </summary>
    public IHost CreateWorkerHost(Action<IServiceCollection>? configureServices = null) =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TimeForYou"] = _postgres.GetConnectionString(),
                    ["WEBUNTIS_BASE_URL"] = MockBaseUrl,
                    ["AES_KEY"] = TestAesKey,
                }))
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IPushSender>(PushSender);
                services.AddWorkerModule(context.Configuration, scheduleRecurringJob: false);
                configureServices?.Invoke(services);
            })
            .Build();

    /// <summary>
    /// The full worker web host (#33) on an in-memory test server: Hangfire server, recurring poll,
    /// <c>/nudge</c> and the dashboard, against this fixture's Postgres and mock, with
    /// <see cref="PushSender"/> in place of the real sender. The recurring poll is set to 600s so it
    /// never fires on its own during a test.
    /// </summary>
    public WebApplication CreateWorkerWebApp() =>
        TimeForYou.Worker.WorkerApp.Build([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TimeForYou"] = _postgres.GetConnectionString(),
                ["WEBUNTIS_BASE_URL"] = MockBaseUrl,
                ["AES_KEY"] = TestAesKey,
                ["POLL_INTERVAL_SECONDS"] = "600",
            });
            builder.Services.AddSingleton<IPushSender>(PushSender);
            // TestServer requests carry no remote address; give them a loopback one.
            builder.Services.AddTransient<IStartupFilter, LoopbackRemoteAddressStartupFilter>();
        });

    private sealed class LoopbackRemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= System.Net.IPAddress.Loopback;
                return nextMiddleware();
            });
            next(app);
        };
    }

    /// <summary>
    /// Truncates every app table so the next test starts from an empty database. Does not touch
    /// the mock's own state — #28 hasn't landed its reset endpoint yet.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "truncate table change_log, ics_tokens, notes, lesson_snapshots, devices, users restart identity cascade");
        
        // First-visit polls the api enqueued in an earlier test (#58) must not be picked up by a later
        // test's worker web app (#33). Hangfire creates its schema lazily, so it may not exist yet.
        await context.Database.ExecuteSqlRawAsync(
            "do $$ begin if to_regclass('hangfire.jobqueue') is not null then truncate table hangfire.jobqueue; end if; end $$");
        PushSender.Clear();
    }
}