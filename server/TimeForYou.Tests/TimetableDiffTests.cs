using TimeForYou.Domain;
using Xunit;

namespace TimeForYou.Tests;

public class TimetableDiffTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 5, 9, 0, 0, TimeSpan.FromHours(2));

    private static Lesson MakeLesson(
        string id,
        string subject = "Maths",
        string? room = "A10",
        DateTimeOffset? start = null,
        LessonStatus status = LessonStatus.Scheduled,
        string? lecturer = "Smith")
    {
        var s = start ?? Nine;
        return new Lesson(id, subject, lecturer, room, s, s.AddHours(1), status);
    }

    [Fact]
    public void No_changes_returns_empty()
    {
        var lessons = new[] { MakeLesson("1"), MakeLesson("2", start: Nine.AddHours(2)) };

        var changes = TimetableDiff.Compute(lessons, lessons);

        Assert.Empty(changes);
    }

    [Fact]
    public void Room_change_returns_one()
    {
        var before = MakeLesson("1");
        var now = before with { Room = "B12" };

        var change = Assert.Single(TimetableDiff.Compute([before], [now]));

        Assert.Equal(ChangeType.Room, change.ChangeType);
        Assert.Equal("A10", change.OldValue);
        Assert.Equal("B12", change.NewValue);
    }

    [Fact]
    public void Start_shift_returns_one()
    {
        var before = MakeLesson("1");
        var now = before with { StartTs = Nine.AddMinutes(30) };

        var change = Assert.Single(TimetableDiff.Compute([before], [now]));

        Assert.Equal(ChangeType.Time, change.ChangeType);
    }

    [Fact]
    public void Teacher_name_change_returns_none()
    {
        var before = MakeLesson("1", lecturer: "Smith");
        var now = before with { Lecturer = "Jones" };

        Assert.Empty(TimetableDiff.Compute([before], [now]));
    }

    [Fact]
    public void Group_and_mode_changes_return_none()
    {
        var before = MakeLesson("1") with { Group = "Group A", Mode = "in_person" };
        var now = before with { Group = "Group B", Mode = "online" };

        Assert.Empty(TimetableDiff.Compute([before], [now]));
    }

    [Fact]
    public void Cancellation_returns_one_with_status_diff()
    {
        var before = MakeLesson("1");
        var now = before with { Status = LessonStatus.Cancelled };

        var change = Assert.Single(TimetableDiff.Compute([before], [now]));

        Assert.Equal(ChangeType.Cancelled, change.ChangeType);
        Assert.Equal("scheduled", change.OldValue);
        Assert.Equal("cancelled", change.NewValue);
    }

    [Fact]
    public void New_lesson_returns_added()
    {
        var change = Assert.Single(TimetableDiff.Compute([], [MakeLesson("1")]));

        Assert.Equal(ChangeType.Added, change.ChangeType);
    }

    [Fact]
    public void Lesson_disappears_returns_removed()
    {
        var change = Assert.Single(TimetableDiff.Compute([MakeLesson("1")], []));

        Assert.Equal(ChangeType.Removed, change.ChangeType);
    }

    [Fact]
    public void Multiple_simultaneous_changes_are_all_captured()
    {
        var a = MakeLesson("1");
        var b = MakeLesson("2", start: Nine.AddHours(2));
        var c = MakeLesson("3", start: Nine.AddHours(4));

        var changes = TimetableDiff.Compute(
            [a, b, c],
            [a with { Room = "B12" }, b with { Status = LessonStatus.Cancelled }, c with { StartTs = c.StartTs.AddMinutes(15) }]);

        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, x => x.UntisLessonId == "1" && x.ChangeType == ChangeType.Room);
        Assert.Contains(changes, x => x.UntisLessonId == "2" && x.ChangeType == ChangeType.Cancelled);
        Assert.Contains(changes, x => x.UntisLessonId == "3" && x.ChangeType == ChangeType.Time);
    }

    [Fact]
    public void Lead_ordering_is_cancellation_then_room_then_time()
    {
        // The time change is earliest, so this proves priority beats start time.
        var timeLesson = MakeLesson("1", start: Nine);
        var roomLesson = MakeLesson("2", start: Nine.AddHours(2));
        var cancelLesson = MakeLesson("3", start: Nine.AddHours(4));

        var changes = TimetableDiff.Compute(
            [timeLesson, roomLesson, cancelLesson],
            [timeLesson with { StartTs = Nine.AddMinutes(30) },
             roomLesson with { Room = "B12" },
             cancelLesson with { Status = LessonStatus.Cancelled }]);

        Assert.Equal(ChangeType.Cancelled, TimetableDiff.SelectLead(changes)!.ChangeType);

        var withoutCancel = changes.Where(x => x.ChangeType != ChangeType.Cancelled).ToList();
        Assert.Equal(ChangeType.Room, TimetableDiff.SelectLead(withoutCancel)!.ChangeType);
    }

    [Fact]
    public void Room_and_time_on_one_lesson_counts_once_per_kind_with_room_lead()
    {
        var before = MakeLesson("1");
        var now = before with { Room = "B12", StartTs = Nine.AddHours(1), EndTs = Nine.AddHours(2) };

        var changes = TimetableDiff.Compute([before], [now]);

        Assert.Equal(2, changes.Count);
        Assert.Single(changes, x => x.ChangeType == ChangeType.Room);
        Assert.Single(changes, x => x.ChangeType == ChangeType.Time);
        Assert.Equal(ChangeType.Room, TimetableDiff.SelectLead(changes)!.ChangeType);
    }

    [Fact]
    public void Notification_text_for_one_change()
    {
        var before = MakeLesson("1");
        var changes = TimetableDiff.Compute([before], [before with { Room = "B12" }]);

        Assert.Equal("Maths moved to room B12", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_for_five_changes()
    {
        var before = new List<Lesson> { MakeLesson("1", subject: "Maths", start: Nine) };
        for (var i = 2; i <= 5; i++)
        {
            before.Add(MakeLesson(i.ToString(), subject: $"Subject{i}", start: Nine.AddHours(i)));
        }

        var now = before.Select(l => l with { Room = "B12" }).ToList();

        var changes = TimetableDiff.Compute(before, now);

        Assert.Equal(5, changes.Count);
        Assert.Equal("Maths moved to room B12 (+4 more changes)", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Lead_tie_on_change_type_goes_to_earliest_start()
    {
        var early = MakeLesson("9", start: Nine);
        var late = MakeLesson("1", start: Nine.AddHours(2));

        var changes = TimetableDiff.Compute(
            [late, early],
            [late with { Room = "B12" }, early with { Room = "B12" }]);

        Assert.Equal("9", TimetableDiff.SelectLead(changes)!.UntisLessonId);
    }

    [Fact]
    public void Lead_tie_on_type_and_start_goes_to_lowest_lesson_id()
    {
        var b = MakeLesson("b");
        var a = MakeLesson("a");

        var changes = TimetableDiff.Compute(
            [b, a],
            [b with { Room = "B12" }, a with { Room = "B12" }]);

        Assert.Equal("a", TimetableDiff.SelectLead(changes)!.UntisLessonId);
    }

    [Fact]
    public void Uncancel_is_logged_as_cancelled_type_with_scheduled_new_value()
    {
        var before = MakeLesson("1", status: LessonStatus.Cancelled);
        var now = before with { Status = LessonStatus.Scheduled };

        var change = Assert.Single(TimetableDiff.Compute([before], [now]));

        Assert.Equal(ChangeType.Cancelled, change.ChangeType);
        Assert.Equal("cancelled", change.OldValue);
        Assert.Equal("scheduled", change.NewValue);
        Assert.Equal("Maths status changed to scheduled", TimetableDiff.BuildNotificationText([change]));
    }

    [Fact]
    public void Scheduled_to_irregular_is_logged_as_cancelled_type()
    {
        var before = MakeLesson("1");
        var now = before with { Status = LessonStatus.Irregular };

        var change = Assert.Single(TimetableDiff.Compute([before], [now]));

        Assert.Equal(ChangeType.Cancelled, change.ChangeType);
        Assert.Equal("irregular", change.NewValue);
        Assert.Equal("Maths status changed to irregular", TimetableDiff.BuildNotificationText([change]));
    }

    [Fact]
    public void Notification_text_for_empty_list_is_null()
    {
        Assert.Null(TimetableDiff.BuildNotificationText([]));
    }

    [Fact]
    public void Notification_text_for_cancellation()
    {
        var before = MakeLesson("1");
        var changes = TimetableDiff.Compute([before], [before with { Status = LessonStatus.Cancelled }]);

        Assert.Equal("Maths has been cancelled", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_for_room_cleared()
    {
        var before = MakeLesson("1");
        var changes = TimetableDiff.Compute([before], [before with { Room = null }]);

        Assert.Equal("Maths no longer has a room assigned", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_for_time_change()
    {
        var before = MakeLesson("1");
        var changes = TimetableDiff.Compute([before], [before with { StartTs = Nine.AddMinutes(30) }]);

        Assert.Equal("Maths has been rescheduled", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_for_added_lesson()
    {
        var changes = TimetableDiff.Compute([], [MakeLesson("1")]);

        Assert.Equal("New lesson: Maths", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_for_removed_lesson()
    {
        var changes = TimetableDiff.Compute([MakeLesson("1")], []);

        Assert.Equal("Maths was removed from your timetable", TimetableDiff.BuildNotificationText(changes));
    }

    [Fact]
    public void Notification_text_mixes_lead_selection_with_count()
    {
        var maths = MakeLesson("1", subject: "Maths", start: Nine);
        var physics = MakeLesson("2", subject: "Physics", start: Nine.AddHours(2));
        var art = MakeLesson("3", subject: "Art", start: Nine.AddHours(4));

        var changes = TimetableDiff.Compute(
            [maths, physics, art],
            [maths with { Room = "B12" },
             physics with { Status = LessonStatus.Cancelled },
             art with { StartTs = art.StartTs.AddMinutes(15) }]);

        Assert.Equal("Physics has been cancelled (+2 more changes)", TimetableDiff.BuildNotificationText(changes));
    }
}