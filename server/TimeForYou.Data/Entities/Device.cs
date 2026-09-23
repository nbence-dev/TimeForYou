using TimeForYou.Domain;

namespace TimeForYou.Data.Entities;

public class Device
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DevicePlatform Platform { get; set; }

    public required string PushToken { get; set; }

    /// <summary>SHA-256 hash of the bearer token returned by `/qr-scan`. The raw token is never stored.</summary>
    public required string BearerTokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
