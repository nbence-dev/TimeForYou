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
}