namespace TimeForYou.Data.Entities;

public class Note
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid LessonId { get; set; }

    public LessonSnapshot Lesson { get; set; } = null!;

    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
