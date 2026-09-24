using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeForYou.Data.Entities;

namespace TimeForYou.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes", t => t.HasCheckConstraint("ck_notes_body_not_empty", "char_length(body) > 0"));

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(n => n.UserId)
            .HasColumnName("user_id");

        builder.Property(n => n.LessonId)
            .HasColumnName("lesson_id");

        // NoteWriteRequest.body: minLength 1, maxLength 500 (docs/openapi.yaml).
        builder.Property(n => n.Body)
            .HasColumnName("body")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(n => n.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notes)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Lesson)
            .WithMany(l => l.Notes)
            .HasForeignKey(n => n.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
