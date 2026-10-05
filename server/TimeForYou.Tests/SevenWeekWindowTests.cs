using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// The seven-week window (#59), end to end: the real mock, the real worker and the real
/// GET /timetable, all reading the clock this test fixes.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class SevenWeekWindowTests(SeamAFixture fixture) : IAsyncLifetime
{
    // Wednesday: the window is Monday 2026-10-05 through Sunday 2026-11-22.
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimetableWindow Window = TimetableWindow.Containing(Now);

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanWire(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("user_id")] Guid UserId);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    private async Task<(string Token, Guid UserId)> OnboardDemoUserAsync()
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        var scan = (await response.Content.ReadFromJsonAsync<QrScanWire>())!;

        // Registering a push token is #36's job; give the device one directly so pushes are captured.
        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync(d => d.UserId == scan.UserId);
        device.PushToken = "test-push-token";
        device.Platform = Enum.GetValues<DevicePlatform>()[0];
        await db.SaveChangesAsync();

        return (scan.Token, scan.UserId);
    }

    private async Task PollAsync()
    {
        using var host = fixture.CreateWorkerHost(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now)));
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }

    private async Task<JsonElement[]> GetTimetableAsync(string bearerToken)
    {
        using var factory = fixture.CreateApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return (await client.GetFromJsonAsync<JsonElement[]>("/timetable"))!;
    }

    private static int WeekIndex(DateTimeOffset start) =>
        (int)Math.Floor((start - Window.StartsAt).TotalDays / 7);

    [Fact]
    public async Task After_a_poll_timetable_spans_all_seven_weeks_and_nothing_beyond()
    {
        var (token, userId) = await OnboardDemoUserAsync();
        await PollAsync();

        var lessons = await GetTimetableAsync(token);

        var starts = lessons.Select(l => l.GetProperty("start_ts").GetDateTimeOffset()).ToList();
        Assert.All(starts, s => Assert.InRange(s, Window.StartsAt, Window.EndsBefore.AddTicks(-1)));
        Assert.Equal(Enumerable.Range(0, 7), starts.Select(WeekIndex).Distinct().Order());

        // The poll stores exactly what the API serves: no weeks fetched that nobody can see.
        await using var db = fixture.CreateDbContext();
        Assert.Equal(await db.LessonSnapshots.CountAsync(l => l.UserId == userId), lessons.Length);
    }

    [Fact]
    public async Task A_user_upgrading_from_the_two_week_window_gets_no_push_for_the_new_weeks()
    {
        var (token, userId) = await OnboardDemoUserAsync();
        await PollAsync();
        await using (var db = fixture.CreateDbContext())
        {
            // Put the user back where the two-week poll left them: only weeks 1-2 stored, and the
            // "seeded through" marker at the end of week 2.
            var twoWeeksIn = Window.StartsAt.AddDays(14);
            await db.LessonSnapshots.Where(l => l.UserId == userId && l.StartTs >= twoWeeksIn).ExecuteDeleteAsync();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.SeededThrough = Window.FirstDay.AddDays(13);
            await db.SaveChangesAsync();
        }

        fixture.PushSender.Clear();
        await PollAsync();

        await using var after = fixture.CreateDbContext();
        Assert.Empty(fixture.PushSender.Sent);
        Assert.Empty(await after.ChangeLog.ToListAsync());
        Assert.Equal(Enumerable.Range(0, 7), (await GetTimetableAsync(token))
            .Select(l => WeekIndex(l.GetProperty("start_ts").GetDateTimeOffset())).Distinct().Order());
    }

    [Fact]
    public async Task A_cancellation_in_week_five_is_detected_and_notified_once()
    {
        await OnboardDemoUserAsync();
        await PollAsync();

        LessonSnapshot weekFive;
        await using (var db = fixture.CreateDbContext())
        {
            weekFive = (await db.LessonSnapshots.OrderBy(l => l.StartTs).ToListAsync())
                .First(l => WeekIndex(l.StartTs) == 4);
        }

        using (var mock = CreateMockClient())
        {
            (await mock.PostAsync($"/mock/cancel-lesson/{weekFive.UntisLessonId}", null)).EnsureSuccessStatusCode();
        }

        fixture.PushSender.Clear();
        await PollAsync();

        await using var after = fixture.CreateDbContext();
        var change = Assert.Single(await after.ChangeLog.ToListAsync());
        Assert.Equal(ChangeType.Cancelled, change.ChangeType);
        Assert.Equal(weekFive.Id, change.LessonId);
        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal($"{weekFive.Subject} has been cancelled", payload.Body);
    }
}
