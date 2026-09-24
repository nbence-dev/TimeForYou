namespace TimeForYou.Data.Entities;

/// <summary>The opaque 64-character calendar-feed path token — one per user.</summary>
public class IcsToken
{
    public required string Token { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
