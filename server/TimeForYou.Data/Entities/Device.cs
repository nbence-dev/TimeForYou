using TimeForYou.Domain;

namespace TimeForYou.Data.Entities;

public class Device
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Null from the QR scan that creates this row (issue #31) until the device's first
    /// <c>POST /devices</c> call (#36) sets it — the app doesn't know its own FCM platform at
    /// scan time.
    /// </summary>
    public DevicePlatform? Platform { get; set; }

    /// <summary>Null until #36 registers a push token; a device exists from its QR scan onward
    /// but only gets one later.</summary>
    public string? PushToken { get; set; }

    /// <summary>SHA-256 hash of the bearer token returned by `/qr-scan`. The raw token is never stored.</summary>
    public required string BearerTokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
