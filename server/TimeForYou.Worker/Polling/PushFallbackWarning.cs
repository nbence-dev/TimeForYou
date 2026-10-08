namespace TimeForYou.Worker.Polling;

/// <summary>
/// Logs once at startup that pushes are only being logged, so a wrong credentials path on
/// showcase day is visible before the first phone fails to buzz (#95).
/// </summary>
public sealed class PushFallbackWarning(ILogger<PushFallbackWarning> logger, string? credentialsPath) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "GOOGLE_APPLICATION_CREDENTIALS ({Path}) is unset or not a file; push notifications will be logged, not sent",
            credentialsPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
