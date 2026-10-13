namespace TimeForYou.Domain;

/// <summary>
/// Compares two sets of lessons and returns the material changes between them.
/// </summary>
/// <remarks>
/// Material fields are StartTs, EndTs, Room and Status; Lecturer, Group, Mode and Subject changes are ignored.
/// Each lesson produces at most one change per kind: a start and end shift together is one Time change,
/// while a room and time change on the same lesson is one Room change plus one Time change.
/// Lead-change rule: the lowest ChangeType wins (Cancelled &gt; Room &gt; Time &gt; Added &gt; Removed);
/// ties go to the earliest StartTs, then the UntisLessonId, so the result is deterministic.
/// </remarks>
public static class TimetableDiff
{
    public static List<LessonChange> Compute(
        IEnumerable<Lesson> previous,
        IEnumerable<Lesson> incoming)
    {
        var beforeById = previous.ToDictionary(l => l.UntisLessonId);
        var nowById = incoming.ToDictionary(l => l.UntisLessonId);
        var changes = new List<LessonChange>();

        foreach (var (id, now) in nowById)
        {
            if (!beforeById.TryGetValue(id, out var before))
            {
                changes.Add(new LessonChange(id, now.Subject, now.StartTs, ChangeType.Added, null, null));
                continue;
            }

            if (before.Status != now.Status)
            {
                changes.Add(new LessonChange(id, now.Subject, now.StartTs, ChangeType.Cancelled,
                    before.Status.ToWireValue(), now.Status.ToWireValue()));
            }

            if (before.Room != now.Room)
            {
                changes.Add(new LessonChange(id, now.Subject, now.StartTs, ChangeType.Room,
                    before.Room, now.Room));
            }

            if (before.StartTs != now.StartTs || before.EndTs != now.EndTs)
            {
                changes.Add(new LessonChange(id, now.Subject, now.StartTs, ChangeType.Time,
                    FormatTime(before), FormatTime(now)));
            }
        }

        foreach (var (id, before) in beforeById)
        {
            if (!nowById.ContainsKey(id))
            {
                changes.Add(new LessonChange(id, before.Subject, before.StartTs, ChangeType.Removed, null, null));
            }
        }

        return changes;
    }

    public static LessonChange? SelectLead(IReadOnlyCollection<LessonChange> changes)
    {
        return changes
            .OrderBy(c => c.ChangeType)
            .ThenBy(c => c.StartTs)
            .ThenBy(c => c.UntisLessonId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static string? BuildNotificationText(IReadOnlyCollection<LessonChange> changes)
    {
        var lead = SelectLead(changes);
        if (lead is null)
        {
            return null;
        }

        var text = Describe(lead);
        var others = changes.Count - 1;
        if (others > 0)
        {
            text += $" (+{others} more changes)";
        }

        return text;
    }

    private static string Describe(LessonChange change) => change.ChangeType switch
    {
        ChangeType.Cancelled => change.NewValue == "cancelled"
            ? $"{change.Subject} has been cancelled"
            : $"{change.Subject} status changed to {change.NewValue}",
        ChangeType.Room => change.NewValue is not null
            ? $"{change.Subject} moved to room {change.NewValue}"
            : $"{change.Subject} no longer has a room assigned",
        ChangeType.Time => $"{change.Subject} has been rescheduled",
        ChangeType.Added => $"New lesson: {change.Subject}",
        ChangeType.Removed => $"{change.Subject} was removed from your timetable",
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    private static string FormatTime(Lesson lesson) => $"{lesson.StartTs:O}/{lesson.EndTs:O}";
}