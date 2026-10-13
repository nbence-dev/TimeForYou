namespace TimeForYou.Domain;

public sealed record Lesson(
    string UntisLessonId,
    string Subject,
    string? Lecturer,
    string? Room,
    DateTimeOffset StartTs,
    DateTimeOffset EndTs,
    LessonStatus Status,
    string? Group = null,
    string? Mode = null);