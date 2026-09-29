using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.UserId)
            .HasColumnName("user_id");

        builder.Property(d => d.Platform)
            .HasColumnName("platform")
            .HasConversion(
                p => p.HasValue ? p.Value.ToWireValue() : null,
                v => v == null ? (DevicePlatform?)null : DevicePlatformExtensions.ParseWireValue(v));

        builder.Property(d => d.PushToken)
            .HasColumnName("push_token");

        builder.Property(d => d.BearerTokenHash)
            .HasColumnName("bearer_token_hash")
            .IsRequired();
        builder.HasIndex(d => d.BearerTokenHash).IsUnique();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(d => d.LastSeenAt)
            .HasColumnName("last_seen_at")
            .HasDefaultValueSql("now()");

        // Deleting a device must not delete the user (acceptance criteria, #14); the reverse
        // direction — deleting a user cascades its devices — is the sane default for owned data.
        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
