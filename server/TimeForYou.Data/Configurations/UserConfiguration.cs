using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;

namespace TimeForYou.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.School)
            .HasColumnName("school")
            .IsRequired();

        builder.Property(u => u.CredentialsProtected)
            .HasColumnName("credentials_protected")
            .IsRequired();

        builder.Property(u => u.UntisUserHash)
            .HasColumnName("untis_user_hash")
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(u => u.UntisUserHash).IsUnique();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(u => u.SeededThrough)
            .HasColumnName("seeded_through");

        // Existing users start with alerts on. The sentinel is `true` (matching the default in
        // User.cs) so EF still writes an explicit `false`: with EF's usual `false` sentinel it would
        // skip sending `false` and the database default would switch alerts back on.
        builder.Property(u => u.ChangeAlertsEnabled)
            .HasColumnName("change_alerts_enabled")
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200);

        builder.Property(u => u.ClassName)
            .HasColumnName("class_name")
            .HasMaxLength(100);
    }

    
}
