using Hangfire;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.WebUntis;

namespace TimeForYou.Worker.Polling;

/// <summary>
/// One poll cycle (issue #32): for every user, fetch this week and next from WebUntis, diff against
/// lesson_snapshots with <see cref="TimetableDiff"/>, upsert the snapshots, write one change_log row
/// per material change, and send one grouped notification to each of the user's push devices.
/// </summary>
/// <remarks>
/// No re-notify (story 16): snapshots and change_log rows are saved before anything is pushed, so the
/// next cycle — even after a worker restart — diffs against the already-updated state. Lessons the
/// worker has never seen in weeks it hasn't polled before (a user's first poll, or the week that
/// enters the rolling window each Monday) are seeded silently; otherwise they'd be reported as "added".
/// </remarks>
public sealed class PollJob(IServiceScopeFactory scopeFactory, ILogger<PollJob> logger, TimeProvider timeProvider) : IUserPollJob
{
    public const string RecurringJobId = "poll-timetables";

    public const string NotificationTitle = "Timetable changed";

    /// <summary>change_log retention (issue #3): older rows are pruned at the start of each cycle.</summary>
    public static readonly TimeSpan ChangeLogRetention = TimeSpan.FromDays(30);

    /// <summary>Eduvos is in South Africa (UTC+2, no DST) — the same offset the mock uses, so both
    /// sides agree on which Monday "this week" starts on.</summary>
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);

    /// <summary>Overlapping runs (a Hangfire retry still going when the next schedule fires) would both
    /// diff the same stale snapshots and push twice, so a second run waits briefly and then gives up.</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 10)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        List<Guid> userIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TimeForYouDbContext>();
            var cutoff = now - ChangeLogRetention;
            await db.ChangeLog.Where(c => c.DetectedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
            userIds = await db.Users.Select(u => u.Id).ToListAsync(cancellationToken);
        }

        var unavailable = 0;
        foreach (var userId in userIds)
        {
            try
            {
                await PollUserAsync(userId, now, cancellationToken);
            }
            catch (WebUntisUnavailableException exception)
            {
                unavailable++;
                logger.LogWarning(exception, "WebUntis was unavailable while polling user {UserId}", userId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Bad credentials or a broken row for one user must not stop the others being polled.
                logger.LogError(exception, "Polling user {UserId} failed; continuing with the next user", userId);
            }
        }

        if (unavailable > 0)
        {
            // Transient: fail the job so Hangfire retries the cycle with backoff. Users that already
            // succeeded are safe to poll again — their snapshots are current, so nothing re-notifies.
            throw new WebUntisUnavailableException($"WebUntis was unavailable for {unavailable} user(s) this cycle.");
        }
    }

    /// <summary>
    /// Polls one user straight away (#58): the api enqueues this when a QR scan creates a new user, so
    /// their timetable fills within seconds instead of at the next recurring cycle. A first poll is
    /// silent (SeededThrough is null), so it never pushes. Failures go to Hangfire's retry, and the
    /// recurring poll still covers the user either way.
    /// </summary>
    public Task RunForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        PollUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);

    private async Task PollUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // A fresh scope (and so a fresh DbContext) per user: one user's failed save can't leave
        // half-tracked entities behind for the next user.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeForYouDbContext>();
        var webUntis = scope.ServiceProvider.GetRequiredService<IWebUntisClient>();
        var protector = scope.ServiceProvider.GetRequiredService<WebUntisCredentialProtector>();
        var pushSender = scope.ServiceProvider.GetRequiredService<IPushSender>();

        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var credentials = protector.Unprotect(user.CredentialsProtected);

        var (from, to) = FetchWindow(now);
        var session = await webUntis.LoginAsync(credentials, cancellationToken);
        var fetched = await webUntis.GetTimetableAsync(session, from, to, cancellationToken);
        var incoming = fetched.Select(FromWebUntis).ToList();

        var snapshots = await db.LessonSnapshots
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.UntisLessonId, cancellationToken);

        var windowStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), Sast);
        var windowEnd = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), Sast);
        var alreadyRemoved = await LessonsLastLoggedAsRemovedAsync(db, userId, cancellationToken);
        var previous = snapshots.Values
            .Where(s => s.StartTs >= windowStart && s.StartTs < windowEnd && !alreadyRemoved.Contains(s.Id))
            .Select(FromSnapshot)
            .ToList();

        // A lesson the worker has never seen, dated after the last window it polled, is in a week that
        // just entered the window (or the user has never been polled): seed it, don't report it.
        // The marker is on the user rather than inferred from "no snapshots", so a first poll that
        // returned no lessons still counts as done and the first real lesson afterwards is reported.
        var silentlySeeded = incoming
            .Where(l => !snapshots.ContainsKey(l.UntisLessonId) && IsBeyondSeededWindow(l, user.SeededThrough))
            .Select(l => l.UntisLessonId)
            .ToHashSet();
        var changes = TimetableDiff.Compute(previous, incoming.Where(l => !silentlySeeded.Contains(l.UntisLessonId)));

        // Upsert on (user_id, untis_lesson_id). A removed lesson's row is left as-is: change_log
        // references it, so it is never deleted.
        foreach (var lesson in incoming)
        {
            if (!snapshots.TryGetValue(lesson.UntisLessonId, out var snapshot))
            {
                snapshot = new LessonSnapshot { UserId = userId, UntisLessonId = lesson.UntisLessonId, Subject = lesson.Subject };
                db.LessonSnapshots.Add(snapshot);
                snapshots[lesson.UntisLessonId] = snapshot;
            }

            snapshot.Subject = lesson.Subject;
            snapshot.Lecturer = lesson.Lecturer;
            snapshot.Room = lesson.Room;
            snapshot.StartTs = lesson.StartTs;
            snapshot.EndTs = lesson.EndTs;
            snapshot.Status = lesson.Status;
            if (db.Entry(snapshot).State == EntityState.Modified)
            {
                snapshot.UpdatedAt = now;
            }
        }

        if (user.SeededThrough is null || user.SeededThrough < to)
        {
            user.SeededThrough = to;
        }

        foreach (var change in changes)
        {
            db.ChangeLog.Add(new ChangeLogEntry
            {
                UserId = userId,
                Lesson = snapshots[change.UntisLessonId],
                ChangeType = change.ChangeType,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                DetectedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (TimetableDiff.BuildNotificationText(changes) is not { } body)
        {
            return;
        }

        var devices = await db.Devices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            if (device is { PushToken: { } pushToken, Platform: { } platform })
            {
                // Already saved, so a failed send is not retried (story 16). One bad token must not
                // stop the user's other devices being told.
                try
                {
                    await pushSender.SendAsync(
                        new PushNotification(userId, device.Id, platform, pushToken, NotificationTitle, body, changes.Count),
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Push to device {DeviceId} of user {UserId} failed; continuing with the next device", device.Id, userId);
                }
            }
        }
    }

    private static bool IsBeyondSeededWindow(Lesson lesson, DateOnly? seededThrough) =>
        seededThrough is null || DateOnly.FromDateTime(lesson.StartTs.ToOffset(Sast).DateTime) > seededThrough;

    /// <summary>This week's Monday through next week's Sunday, in SAST (issue #32: "this week + next").</summary>
    private static (DateOnly From, DateOnly To) FetchWindow(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.ToOffset(Sast).Date);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(13));
    }

    /// <summary>
    /// Lessons whose most recent change_log row is "removed" were already reported gone. They're left
    /// out of the previous set so the next cycle doesn't report them removed again (story 16); if one
    /// comes back, the diff reports it as "added".
    /// </summary>
    private static async Task<HashSet<Guid>> LessonsLastLoggedAsRemovedAsync(
        TimeForYouDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var entries = await db.ChangeLog
            .Where(c => c.UserId == userId)
            .Select(c => new { c.LessonId, c.ChangeType, c.DetectedAt })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(e => e.LessonId)
            .Where(g => g.MaxBy(e => e.DetectedAt)!.ChangeType == ChangeType.Removed)
            .Select(g => g.Key)
            .ToHashSet();
    }

    // Postgres timestamptz only accepts UTC DateTimeOffsets, so incoming times are normalised to UTC.
    private static Lesson FromWebUntis(WebUntisLesson lesson) => new(
        lesson.UntisLessonId,
        lesson.SubjectName,
        lesson.Lecturer,
        lesson.Room,
        lesson.StartTs.ToUniversalTime(),
        lesson.EndTs.ToUniversalTime(),
        lesson.Status);

    private static Lesson FromSnapshot(LessonSnapshot snapshot) => new(
        snapshot.UntisLessonId,
        snapshot.Subject,
        snapshot.Lecturer,
        snapshot.Room,
        snapshot.StartTs,
        snapshot.EndTs,
        snapshot.Status);
}
