   namespace TimeForYou.Domain;

   public sealed record LessonChange(
       string UntisLessonId,
       string Subject,
       DateTimeOffset StartTs,
       ChangeType ChangeType,
       string? OldValue,
       string? NewValue);