using System.Collections.Concurrent;
using TimeForYou.Domain;

namespace TimeForYou.Tests.Infrastructure;

/// <summary>
/// Stands in for the real FCM-backed <see cref="IPushSender"/> (Week 3) in Seam A tests: records
/// every payload it is asked to send instead of dispatching it, so assertions can inspect what
/// the poll job would have pushed.
/// </summary>
public sealed class CapturingPushSender : IPushSender
{
    private readonly ConcurrentQueue<PushNotification> _sent = new();

    public IReadOnlyCollection<PushNotification> Sent => _sent;

    public Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(notification);
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();
}
