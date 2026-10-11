using TimeForYou.Domain;

namespace TimeForYou.Worker.Polling;

/// <summary>
/// No-credentials fallback <see cref="IPushSender"/>: logs the grouped notification the poll job
/// would have pushed instead of sending it.
/// </summary>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Push (logged, not sent) to user {UserId}, device {DeviceId}: {Title} — {Body}",
            notification.UserId,
            notification.DeviceId,
            notification.Title,
            notification.Body);
        return Task.CompletedTask;
    }
}