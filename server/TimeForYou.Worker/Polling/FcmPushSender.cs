using FirebaseAdmin.Messaging;
using TimeForYou.Domain;

namespace TimeForYou.Worker.Polling;

/// <summary>
/// Firebase-backed <see cref="IPushSender"/> (#95): one <c>FirebaseMessaging.SendAsync</c> call
/// serves both <c>android_fcm</c> and <c>web_push</c> tokens. A token Firebase reports as
/// unregistered is logged and swallowed. Any other failure propagates so the poll job logs it as
/// an error, and that includes a junk token: Firebase answers it with InvalidArgument, the same as
/// a malformed message, so the two cannot be told apart here.
/// </summary>
public sealed class FcmPushSender(FirebaseMessaging messaging, ILogger<FcmPushSender> logger, string? webAppUrl = null) : IPushSender
{
    /// <summary>Where a tapped web notification lands: the Changes screen (#71). FCM requires the link to be absolute https, so it is prefixed with <c>WEB_APP_URL</c>.</summary>
    public const string ChangesLink = "/changes";

    public async Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await messaging.SendAsync(BuildMessage(notification), cancellationToken);
        }
        catch (FirebaseMessagingException exception)
            when (exception.MessagingErrorCode is MessagingErrorCode.Unregistered)
        {
            logger.LogWarning(
                exception,
                "FCM reports the token for device {DeviceId} of user {UserId} as unregistered ({ErrorCode}); skipping it",
                notification.DeviceId,
                notification.UserId,
                exception.MessagingErrorCode);
        }
    }

    public Message BuildMessage(PushNotification notification) => new()
    {
        Token = notification.PushToken,
        Notification = new Notification { Title = notification.Title, Body = notification.Body },
        Webpush = notification.Platform == DevicePlatform.WebPush && webAppUrl is { Length: > 0 }
            ? new WebpushConfig { FcmOptions = new WebpushFcmOptions { Link = webAppUrl.TrimEnd('/') + ChangesLink } }
            : null,
    };
}
