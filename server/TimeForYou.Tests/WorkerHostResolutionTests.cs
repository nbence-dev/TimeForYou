using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TimeForYou.Data;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// Resolution coverage for the worker's DI container in test mode (#27, updated for #32): the poll
/// job and its dependencies resolve, the capturing push sender replaces the logging one, and the
/// recurring schedule is not registered.
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
        using var scope = host.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TimeForYouDbContext>());
        Assert.Same(fixture.PushSender, scope.ServiceProvider.GetRequiredService<IPushSender>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PollJob>());
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service is PollJobScheduler);
    }
}