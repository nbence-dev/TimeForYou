using TimeForYou.Data;

namespace TimeForYou.Worker;

/// <summary>
/// Holds worker startup until the API has applied every migration (#62). Only the API migrates;
/// the worker just waits, so it comes up healthy whichever of the two starts first.
/// </summary>
public sealed class SchemaReadyGate(IServiceProvider services, ILogger<SchemaReadyGate> logger) : IHostedService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public Task StartAsync(CancellationToken cancellationToken) =>
        services.WaitForSchemaAsync(RetryDelay, logger, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
