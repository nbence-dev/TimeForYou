using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;

namespace TimeForYou.Data.Configurations;

public class IcsTokenConfiguration : IEntityTypeConfiguration<IcsToken>
{
    public void Configure(EntityTypeBuilder<IcsToken> builder)
    {
        builder.ToTable("ics_tokens", t => t.HasCheckConstraint("ck_ics_tokens_token_length", "char_length(token) = 64"));

        // The token itself is the lookup key for GET /ics/{token}.ics — no separate surrogate id.
        builder.HasKey(t => t.Token);
        builder.Property(t => t.Token)
            .HasColumnName("token")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id");
        builder.HasIndex(t => t.UserId).IsUnique();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(t => t.User)
            .WithOne(u => u.IcsToken)
            .HasForeignKey<IcsToken>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
