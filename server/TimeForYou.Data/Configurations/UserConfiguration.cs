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
    }
}
