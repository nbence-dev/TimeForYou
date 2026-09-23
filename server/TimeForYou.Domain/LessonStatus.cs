namespace TimeForYou.Domain;

/// <summary>Matches the `status` enum in docs/openapi.yaml's Lesson schema.</summary>
public enum LessonStatus
{
    Scheduled,
    Cancelled,
    Irregular,
}

public static class LessonStatusExtensions
{
    public static string ToWireValue(this LessonStatus status) => status switch
    {
        LessonStatus.Scheduled => "scheduled",
        LessonStatus.Cancelled => "cancelled",
        LessonStatus.Irregular => "irregular",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static LessonStatus ParseWireValue(string value) => value switch
    {
        "scheduled" => LessonStatus.Scheduled,
        "cancelled" => LessonStatus.Cancelled,
        "irregular" => LessonStatus.Irregular,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
