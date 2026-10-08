using FirebaseAdmin.Messaging;
using TimeForYou.Domain;

namespace TimeForYou.Worker.Polling;

/// <summary>
/// Firebase-backed <see cref="IPushSender"/> (#95): one <c>FirebaseMessaging.SendAsync</c> call
/// serves both <c>android_fcm</c> and <c>web_push</c> tokens. A token Firebase rejects as invalid
/// or unregistered is logged and swallowed, so it cannot fail the poll cycle (the job also isolates
/// each device, but a dead token is expected, not an error).
/// </summary>
public sealed class FcmPushSender(FirebaseMessaging messaging, ILogger<FcmPushSender> logger) : IPushSender
{
    /// <summary>Where a tapped web notification lands: the Changes screen (#71).</summary>
    public const string ChangesLink = "/changes";

    public async Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await messaging.SendAsync(BuildMessage(notification), cancellationToken);
        }
        catch (FirebaseMessagingException exception)
            when (exception.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            logger.LogWarning(
                exception,
                "FCM rejected the token for device {DeviceId} of user {UserId} ({ErrorCode}); skipping it",
                notification.DeviceId,
                notification.UserId,
                exception.MessagingErrorCode);
        }
    }

    public static Message BuildMessage(PushNotification notification) => new()
    {
        Token = notification.PushToken,
        Notification = new Notification { Title = notification.Title, Body = notification.Body },
        Webpush = notification.Platform == DevicePlatform.WebPush
            ? new WebpushConfig { FcmOptions = new WebpushFcmOptions { Link = ChangesLink } }
            : null,
    };
}
