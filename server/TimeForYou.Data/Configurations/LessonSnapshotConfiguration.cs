using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Data.Configurations;

public class LessonSnapshotConfiguration : IEntityTypeConfiguration<LessonSnapshot>
{
    public void Configure(EntityTypeBuilder<LessonSnapshot> builder)
    {
        builder.ToTable("lesson_snapshots");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.UserId)
            .HasColumnName("user_id");

        builder.Property(l => l.UntisLessonId)
            .HasColumnName("untis_lesson_id")
            .IsRequired();

        builder.Property(l => l.Subject)
            .HasColumnName("subject")
            .IsRequired();

        builder.Property(l => l.Lecturer)
            .HasColumnName("lecturer");

        builder.Property(l => l.Room)
            .HasColumnName("room");

        builder.Property(l => l.Group)
            .HasColumnName("group_name");

        builder.Property(l => l.Mode)
            .HasColumnName("mode");

        builder.Property(l => l.StartTs)
            .HasColumnName("start_ts")
            .IsRequired();

        builder.Property(l => l.EndTs)
            .HasColumnName("end_ts")
            .IsRequired();

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion(s => s.ToWireValue(), v => LessonStatusExtensions.ParseWireValue(v))
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Acceptance criteria (#14): keeps the poll cycle's per-user upsert cheap and prevents
        // the same upstream lesson being duplicated across polls.
        builder.HasIndex(l => new { l.UserId, l.UntisLessonId }).IsUnique();

        builder.HasOne(l => l.User)
            .WithMany(u => u.LessonSnapshots)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
