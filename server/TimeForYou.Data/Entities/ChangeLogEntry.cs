using TimeForYou.Domain;

namespace TimeForYou.Data.Entities;

public class ChangeLogEntry
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid LessonId { get; set; }

    public LessonSnapshot Lesson { get; set; } = null!;

    public ChangeType ChangeType { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTimeOffset DetectedAt { get; set; }
}
