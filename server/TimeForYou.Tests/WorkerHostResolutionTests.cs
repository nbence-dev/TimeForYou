using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeForYou.Data;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>
/// Placeholder resolution coverage for the worker's DI container (issue #27 scope). The poll job
/// itself arrives in #32 — this only proves the shared Postgres + mock plumbing resolves what
/// exists today.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class WorkerHostResolutionTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Worker_host_resolves_its_registered_services()
    {
        using var host = fixture.CreateWorkerHost();

        var dbContext = host.Services.GetRequiredService<TimeForYouDbContext>();
        var pushSender = host.Services.GetRequiredService<IPushSender>();
        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.NotNull(dbContext);
        Assert.Same(fixture.PushSender, pushSender);
        Assert.Contains(hostedServices, service => service is global::TimeForYou.Worker.Worker);
    }
}
