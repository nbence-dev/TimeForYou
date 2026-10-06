using System.Net.Http.Json;
using System.Reflection;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.WebUntis;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// Seam A coverage for the poll job (#32): real Postgres and mock, users onboarded through
/// POST /qr-scan, the job invoked directly in test mode, pushes captured by
/// <see cref="CapturingPushSender"/>.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class PollJobTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task First_poll_seeds_snapshots_without_notifying()
    {
        await OnboardDemoUserWithPushDeviceAsync();

        await RunPollCycleAsync();

        Assert.NotEmpty(await SnapshotsInOrderAsync());
        Assert.Empty(await ChangeLogAsync());
        Assert.Empty(fixture.PushSender.Sent);
    }

    [Fact]
    public async Task Mock_cancellation_produces_one_change_log_row_and_one_payload()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();
        var lesson = (await SnapshotsInOrderAsync())[0];

        await CancelInMockAsync(lesson.UntisLessonId);
        await RunPollCycleAsync();

        var entry = Assert.Single(await ChangeLogAsync());
        Assert.Equal(ChangeType.Cancelled, entry.ChangeType);
        Assert.Equal("cancelled", entry.NewValue);

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal($"{lesson.Subject} has been cancelled", payload.Body);
        Assert.Equal(1, payload.ChangeCount);
    }

    [Fact]
    public async Task Five_changes_in_one_cycle_still_send_one_payload_ending_plus_four()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();
        var lessons = await SnapshotsInOrderAsync();

        await CancelInMockAsync(lessons[0].UntisLessonId);
        for (var i = 1; i <= 4; i++)
        {
            await ChangeRoomInMockAsync(lessons[i].UntisLessonId, "Z99");
        }

        await RunPollCycleAsync();

        Assert.Equal(5, (await ChangeLogAsync()).Count);
        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.StartsWith($"{lessons[0].Subject} has been cancelled", payload.Body);
        Assert.EndsWith("(+4 more changes)", payload.Body);
        Assert.Equal(5, payload.ChangeCount);
    }

    [Fact]
    public async Task Polling_again_with_no_mock_change_sends_nothing_and_logs_nothing()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();
        await CancelInMockAsync((await SnapshotsInOrderAsync())[0].UntisLessonId);
        await RunPollCycleAsync();
        fixture.PushSender.Clear();

        // A fresh worker host, so this also covers "no re-notify after a worker restart".
        await RunPollCycleAsync();

        Assert.Empty(fixture.PushSender.Sent);
        Assert.Single(await ChangeLogAsync());
    }

    [Fact]
    public async Task A_user_with_bad_credentials_does_not_stop_other_users_being_polled()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();

        using (var host = fixture.CreateWorkerHost())
        {
            var protector = host.Services.GetRequiredService<WebUntisCredentialProtector>();
            await using var db = fixture.CreateDbContext();
            db.Users.Add(new User
            {
                School = "eduvos-demo",
                CredentialsProtected = protector.Protect(
                    new WebUntisCredentials("mock.timeforyou.local", "eduvos-demo", "nobody", "wrong-key")),
                UntisUserHash = "bad-credentials-user",
            });
            await db.SaveChangesAsync();
        }

        var lesson = (await SnapshotsInOrderAsync())[0];
        await CancelInMockAsync(lesson.UntisLessonId);

        await RunPollCycleAsync();

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal($"{lesson.Subject} has been cancelled", payload.Body);
    }

    [Fact]
    public async Task Change_log_rows_older_than_30_days_are_pruned()
    {
        var userId = await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();
        var lesson = (await SnapshotsInOrderAsync())[0];

        await using (var db = fixture.CreateDbContext())
        {
            db.ChangeLog.Add(new ChangeLogEntry
            {
                UserId = userId, LessonId = lesson.Id, ChangeType = ChangeType.Room,
                OldValue = "A1", NewValue = "B2", DetectedAt = DateTimeOffset.UtcNow.AddDays(-31),
            });
            db.ChangeLog.Add(new ChangeLogEntry
            {
                UserId = userId, LessonId = lesson.Id, ChangeType = ChangeType.Room,
                OldValue = "B2", NewValue = "C3", DetectedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        await RunPollCycleAsync();

        var remaining = Assert.Single(await ChangeLogAsync());
        Assert.Equal("C3", remaining.NewValue);
    }

    [Fact]
    public async Task Teacher_only_change_produces_nothing()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();

        // The mock has no endpoint for changing a lecturer, so change the stored one instead: the
        // next poll then sees a lecturer difference and nothing else.
        await using (var db = fixture.CreateDbContext())
        {
            var snapshot = await db.LessonSnapshots.OrderBy(s => s.StartTs).FirstAsync();
            snapshot.Lecturer = "Dr Someone Else";
            await db.SaveChangesAsync();
        }

        await RunPollCycleAsync();

        Assert.Empty(await ChangeLogAsync());
        Assert.Empty(fixture.PushSender.Sent);
    }

    [Fact]
    public async Task A_lesson_moved_out_of_the_window_is_reported_removed_only_once()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await RunPollCycleAsync();
        var lesson = (await SnapshotsInOrderAsync())[0];
        var farAway = DateTimeOffset.UtcNow.AddDays(90); // well past the seven-week window (#59)

        await MoveInMockAsync(lesson.UntisLessonId, farAway, farAway.AddHours(1));
        await RunPollCycleAsync();

        var entry = Assert.Single(await ChangeLogAsync());
        Assert.Equal(ChangeType.Removed, entry.ChangeType);
        Assert.Single(fixture.PushSender.Sent);

        fixture.PushSender.Clear();
        await RunPollCycleAsync();

        Assert.Empty(fixture.PushSender.Sent);
        Assert.Single(await ChangeLogAsync());
    }

    [Fact]
    public async Task The_week_that_enters_the_window_on_monday_is_seeded_silently()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        var sundayNoon = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(2));
        var mondayJustAfterMidnight = new DateTimeOffset(2026, 10, 19, 0, 30, 0, TimeSpan.FromHours(2));

        await RunPollCycleAsync(new FixedTimeProvider(sundayNoon));
        var seededBefore = (await SnapshotsInOrderAsync()).Count;
        await RunPollCycleAsync(new FixedTimeProvider(mondayJustAfterMidnight));

        Assert.True((await SnapshotsInOrderAsync()).Count > seededBefore, "the new week should have been saved");
        Assert.Empty(await ChangeLogAsync());
        Assert.Empty(fixture.PushSender.Sent);
    }

    [Fact]
    public async Task A_lesson_that_appears_after_an_empty_first_poll_is_reported_as_added()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(2)));
        var lesson = new WebUntisLesson(
            "stub-1", "MATH", "Maths", null, "A1", LessonStatus.Scheduled,
            new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(2)));

        await RunPollCycleAsync(clock, new StubWebUntisClient([]));
        Assert.Empty(fixture.PushSender.Sent);

        await RunPollCycleAsync(clock, new StubWebUntisClient([lesson]));

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal("New lesson: Maths", payload.Body);
        Assert.Equal(ChangeType.Added, Assert.Single(await ChangeLogAsync()).ChangeType);
    }

    [Fact]
    public async Task A_failing_push_to_one_device_does_not_stop_the_users_other_devices()
    {
        var userId = await OnboardDemoUserWithPushDeviceAsync();
        await using (var db = fixture.CreateDbContext())
        {
            db.Devices.Add(new Device
            {
                UserId = userId, BearerTokenHash = "second-device", PushToken = FailingPushSender.BadToken,
                Platform = Enum.GetValues<DevicePlatform>()[0],
            });
            await db.SaveChangesAsync();
        }

        await RunPollCycleAsync();
        var lesson = (await SnapshotsInOrderAsync())[0];
        await CancelInMockAsync(lesson.UntisLessonId);

        await RunPollCycleAsync(pushSender: new FailingPushSender(fixture.PushSender));

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal("test-push-token", payload.PushToken);
    }

    [Fact]
    public void Poll_runs_cannot_overlap()
    {
        var run = typeof(PollJob).GetMethod(nameof(PollJob.RunAsync));

        Assert.NotNull(run!.GetCustomAttribute<DisableConcurrentExecutionAttribute>());
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanResponseWire(
        string Token,
        [property: System.Text.Json.Serialization.JsonPropertyName("user_id")] Guid UserId);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    /// <summary>Onboards the mock's demo student through the real POST /qr-scan, then gives the
    /// device a push token directly (registering one is #36's job, not this ticket's).</summary>
    private async Task<Guid> OnboardDemoUserWithPushDeviceAsync()
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        var scan = await response.Content.ReadFromJsonAsync<QrScanResponseWire>()
            ?? throw new InvalidOperationException("qr-scan returned no body");

        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync(d => d.UserId == scan.UserId);
        device.PushToken = "test-push-token";
        device.Platform = Enum.GetValues<DevicePlatform>()[0];
        await db.SaveChangesAsync();

        return scan.UserId;
    }

    /// <summary>A fresh worker host per cycle, so state carries over only through the database —
    /// the same as the worker restarting between polls.</summary>
    private async Task RunPollCycleAsync(
        TimeProvider? clock = null, IWebUntisClient? webUntis = null, IPushSender? pushSender = null)
    {
        using var host = fixture.CreateWorkerHost(services =>
        {
            if (clock is not null) services.AddSingleton(clock);
            if (webUntis is not null) services.AddSingleton(webUntis);
            if (pushSender is not null) services.AddSingleton(pushSender);
        });
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }

    private async Task<List<LessonSnapshot>> SnapshotsInOrderAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.LessonSnapshots.OrderBy(s => s.StartTs).ThenBy(s => s.UntisLessonId).ToListAsync();
    }

    private async Task<List<ChangeLogEntry>> ChangeLogAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.ChangeLog.ToListAsync();
    }

    private async Task CancelInMockAsync(string lessonId)
    {
        using var mock = CreateMockClient();
        (await mock.PostAsync($"/mock/cancel-lesson/{lessonId}", null)).EnsureSuccessStatusCode();
    }

    private async Task ChangeRoomInMockAsync(string lessonId, string room)
    {
        using var mock = CreateMockClient();
        (await mock.PostAsJsonAsync($"/mock/change-room/{lessonId}", new { room })).EnsureSuccessStatusCode();
    }

    private async Task MoveInMockAsync(string lessonId, DateTimeOffset start, DateTimeOffset end)
    {
        using var mock = CreateMockClient();
        (await mock.PostAsJsonAsync($"/mock/move-lesson/{lessonId}", new { start_ts = start, end_ts = end }))
            .EnsureSuccessStatusCode();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed class StubWebUntisClient(IReadOnlyList<WebUntisLesson> lessons) : IWebUntisClient
    {
        public Task<WebUntisSession> LoginAsync(WebUntisCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WebUntisSession("stub"));

        public Task<IReadOnlyList<WebUntisLesson>> GetTimetableAsync(
            WebUntisSession session, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult(lessons);
    }

    private sealed class FailingPushSender(IPushSender inner) : IPushSender
    {
        public const string BadToken = "bad-push-token";

        public Task SendAsync(PushNotification notification, CancellationToken cancellationToken = default) =>
            notification.PushToken == BadToken
                ? throw new InvalidOperationException("FCM rejected the token")
                : inner.SendAsync(notification, cancellationToken);
    }
}
