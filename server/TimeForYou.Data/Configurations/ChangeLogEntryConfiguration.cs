using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Data.Configurations;

public class ChangeLogEntryConfiguration : IEntityTypeConfiguration<ChangeLogEntry>
{
    public void Configure(EntityTypeBuilder<ChangeLogEntry> builder)
    {
        builder.ToTable("change_log");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.UserId)
            .HasColumnName("user_id");

        builder.Property(c => c.LessonId)
            .HasColumnName("lesson_id");

        builder.Property(c => c.ChangeType)
            .HasColumnName("change_type")
            .HasConversion(c => c.ToWireValue(), v => ChangeTypeExtensions.ParseWireValue(v))
            .IsRequired();

        builder.Property(c => c.OldValue)
            .HasColumnName("old_value");

        builder.Property(c => c.NewValue)
            .HasColumnName("new_value");

        builder.Property(c => c.DetectedAt)
            .HasColumnName("detected_at")
            .HasDefaultValueSql("now()");

        // GET /changes reads most-recent-first per caller; the worker's 30-day pruning job
        // (out of scope here, per #14) will scan on the same two columns.
        builder.HasIndex(c => new { c.UserId, c.DetectedAt });

        builder.HasOne(c => c.User)
            .WithMany(u => u.ChangeLogEntries)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: lesson_snapshots rows are never hard-deleted by the poll/diff
        // cycle (a "removed" lesson is a change_log entry, not a row deletion), so change history
        // should never be able to silently disappear because its lesson snapshot went away.
        builder.HasOne(c => c.Lesson)
            .WithMany(l => l.ChangeLogEntries)
            .HasForeignKey(c => c.LessonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
