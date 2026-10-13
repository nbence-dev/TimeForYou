using TimeForYou.Domain;

namespace TimeForYou.Data.Entities;

public class LessonSnapshot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>The upstream WebUntis lesson identifier this snapshot is matched on across polls.</summary>
    public required string UntisLessonId { get; set; }

    public required string Subject { get; set; }

    public string? Lecturer { get; set; }

    public string? Room { get; set; }

    /// <summary>Display-only, like <see cref="Lecturer"/>: never diffed, never notifies (#49).</summary>
    public string? Group { get; set; }

    /// <summary>"online" or "in_person". Display-only (#49).</summary>
    public string? Mode { get; set; }

    public DateTimeOffset StartTs { get; set; }

    public DateTimeOffset EndTs { get; set; }

    public LessonStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Note> Notes { get; set; } = [];

    public List<ChangeLogEntry> ChangeLogEntries { get; set; } = [];
}
