using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ical.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// What a calendar subscriber sees when the university moves a lesson that has already started
/// (issue #37). Drives the real mock, the real worker and the real API feed end to end.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class IcsFeedMoveAfterStartTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanWire([property: JsonPropertyName("token")] string Token);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    private async Task<string> OnboardDemoUserAsync()
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QrScanWire>())!.Token;
    }

    private async Task RunPollCycleAsync()
    {
        using var host = fixture.CreateWorkerHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }

    /// <summary>The feed as a subscribed calendar app would fetch it when the clock reads <paramref name="now"/>.</summary>
    private async Task<Calendar> FetchFeedAtAsync(string bearerToken, DateTimeOffset now)
    {
        using var factory = fixture.CreateApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now))));

        using var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var body = await authed.GetFromJsonAsync<JsonElement>("/calendar");
        var feedPath = new Uri(body.GetProperty("url").GetString()!).AbsolutePath;

        using var anonymous = factory.CreateClient();
        return Calendar.Load(await anonymous.GetStringAsync(feedPath))!;
    }

    [Fact]
    public async Task A_lesson_moved_later_after_it_started_comes_back_in_the_feed_at_its_new_time()
    {
        var bearerToken = await OnboardDemoUserAsync();
        await RunPollCycleAsync();

        await using var db = fixture.CreateDbContext();
        var lesson = await db.LessonSnapshots.OrderBy(s => s.StartTs).FirstAsync();
        var uid = $"{lesson.UntisLessonId}@timeforyou";
        var tenMinutesIn = lesson.StartTs.AddMinutes(10);

        // At 10 minutes in, the lesson has started, so the feed no longer lists it.
        var before = await FetchFeedAtAsync(bearerToken, tenMinutesIn);
        Assert.DoesNotContain(before.Events, e => e.Uid == uid);

        // The university moves it two hours later; the next poll picks that up.
        var newStart = lesson.StartTs.AddHours(2);
        using (var mock = CreateMockClient())
        {
            (await mock.PostAsJsonAsync(
                $"/mock/move-lesson/{lesson.UntisLessonId}",
                new { start_ts = newStart, end_ts = newStart.AddHours(1) })).EnsureSuccessStatusCode();
        }

        await RunPollCycleAsync();

        // Same clock reading, but the lesson now starts in the future: back in the feed, same UID,
        // at the new time, so a calendar app moves the existing event rather than adding one.
        var after = await FetchFeedAtAsync(bearerToken, tenMinutesIn);
        var moved = Assert.Single(after.Events, e => e.Uid == uid);
        Assert.Equal(newStart.UtcDateTime, moved.DtStart!.AsUtc);
    }
}
