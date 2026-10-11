namespace TimeForYou.Worker.Polling;

/// <summary>
/// Logs one push-configuration problem once at startup, so a wrong credentials path or a bad
/// <c>WEB_APP_URL</c> on showcase day is visible before the first phone fails to buzz (#95).
/// </summary>
public sealed class PushStartupNotice(ILogger<PushStartupNotice> logger, LogLevel level, string message) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.Log(level, "{Notice}", message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
