using TimeForYou.Domain;

namespace TimeForYou.WebUntis;

/// <summary>
/// One lesson occurrence as WebUntis reports it, already mapped off the mock's wire shape (issue
/// #30). <see cref="UntisLessonId"/> is per-occurrence, not per-template (mock issue #28), so it is
/// stable across polls for the same calendar occurrence. Stands in for the real domain
/// <c>Lesson</c> type until #29 lands; #32 maps this into that type.
/// </summary>
public sealed record WebUntisLesson(
    string UntisLessonId,
    string SubjectCode,
    string SubjectName,
    string? Lecturer,
    string Room,
    LessonStatus Status,
    DateTimeOffset StartTs,
    DateTimeOffset EndTs,
    string? Group = null,
    string? Mode = null);
