namespace TimeForYou.Domain;

/// <summary>
/// Sends the single grouped FCM notification a poll cycle produces for one user (issue #3
/// §*Change notifications*). The real FCM-backed implementation lands Week 3; until then, Seam A
/// tests substitute a capturing fake that records every payload for assertions.
/// </summary>
public interface IPushSender
{
    Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// One grouped notification: the lead change's own summary plus how many other changes were
/// folded into it (issue #3 story 14, "Maths moved to Room 4B (+3 more changes)").
/// </summary>
public sealed record PushNotification(
    Guid UserId,
    Guid DeviceId,
    DevicePlatform Platform,
    string PushToken,
    string Title,
    string Body,
    int ChangeCount);
