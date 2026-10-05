using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TimeForYou.Data;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>The API applies pending EF migrations before it serves requests (issue #62).</summary>
[Collection(SeamACollection.Name)]
public sealed class MigrateOnStartupTests(SeamAFixture fixture)
{
    private sealed record QrPayload(string Server, string School, string User, string Key);

    private async Task AssertQrScanWorksAsync(HttpClient client)
    {
        using var mockClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var payload = await mockClient.GetFromJsonAsync<QrPayload>("/qr-payload");

        var response = await client.PostAsJsonAsync("/qr-scan", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Starting_against_an_empty_database_migrates_it_and_serves_qr_scan()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using (var before = SeamAFixture.CreateDbContext(connectionString))
        {
            Assert.NotEmpty(await before.Database.GetPendingMigrationsAsync());
        }

        using var factory = fixture.CreateApiFactory(connectionString);
        using var client = factory.CreateClient();

        await AssertQrScanWorksAsync(client);

        await using var after = SeamAFixture.CreateDbContext(connectionString);
        Assert.Empty(await after.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Starting_against_a_database_one_migration_behind_applies_it()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using (var seed = SeamAFixture.CreateDbContext(connectionString))
        {
            var all = seed.GetService<IMigrationsAssembly>().Migrations.Keys.Order().ToList();
            await seed.GetService<IMigrator>().MigrateAsync(all[^2]);
            Assert.Single(await seed.Database.GetPendingMigrationsAsync());
        }

        using var factory = fixture.CreateApiFactory(connectionString);
        using var client = factory.CreateClient();

        await AssertQrScanWorksAsync(client);

        await using var after = SeamAFixture.CreateDbContext(connectionString);
        Assert.Empty(await after.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task A_second_start_with_nothing_pending_changes_nothing()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        using (var first = fixture.CreateApiFactory(connectionString))
        {
            using var _ = first.CreateClient();
        }

        await using var context = SeamAFixture.CreateDbContext(connectionString);
        var appliedBefore = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        using var second = fixture.CreateApiFactory(connectionString);
        using var client = second.CreateClient();
        Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);

        Assert.Equal(appliedBefore, (await context.Database.GetAppliedMigrationsAsync()).ToList());
    }

    [Fact]
    public async Task A_failing_migration_stops_the_api_from_starting()
    {
        // A table the InitialCreate migration wants to create already exists, so applying it fails.
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using (var sabotage = SeamAFixture.CreateDbContext(connectionString))
        {
            await sabotage.Database.ExecuteSqlRawAsync("create table users (id int)");
        }

        using var factory = fixture.CreateApiFactory(connectionString);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var client = factory.CreateClient();
            await client.GetAsync("/health");
        });
    }

    [Fact]
    public async Task Worker_schema_gate_waits_until_the_api_has_migrated()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        using var host = fixture.CreateWorkerHost(services =>
            services.AddDbContext<TimeForYouDbContext>(o => o.UseNpgsql(connectionString)));

        var wait = host.Services.WaitForSchemaAsync(
            TimeSpan.FromMilliseconds(100), NullLogger.Instance);
        await Task.Delay(500);
        Assert.False(wait.IsCompleted);

        await using (var migrator = SeamAFixture.CreateDbContext(connectionString))
        {
            await migrator.Database.MigrateAsync();
        }

        await wait.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
