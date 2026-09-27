namespace TimeForYou.Domain;

/// <summary>
/// Matches the `change_type` enum in docs/openapi.yaml's ChangeLogEntry schema, ordered
/// cancellation > room > time to match the lead-change rule for the grouped FCM notification.
/// </summary>
public enum ChangeType
{
    Cancelled,
    Room,
    Time,
    Added,
    Removed,
}

public static class ChangeTypeExtensions
{
    public static string ToWireValue(this ChangeType changeType) => changeType switch
    {
        ChangeType.Cancelled => "cancelled",
        ChangeType.Room => "room",
        ChangeType.Time => "time",
        ChangeType.Added => "added",
        ChangeType.Removed => "removed",
        _ => throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null),
    };

    public static ChangeType ParseWireValue(string value) => value switch
    {
        "cancelled" => ChangeType.Cancelled,
        "room" => ChangeType.Room,
        "time" => ChangeType.Time,
        "added" => ChangeType.Added,
        "removed" => ChangeType.Removed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}

