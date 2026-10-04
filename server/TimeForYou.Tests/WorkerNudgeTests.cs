using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker;

namespace TimeForYou.Tests;

/// <summary>
/// Seam A coverage for the worker web host (#33): POST /nudge enqueues an immediate poll that runs to
/// completion and pushes, using the real Hangfire server on the fixture's Postgres. The dashboard UI
/// itself is a manual check.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class WorkerNudgeTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Nudge_runs_a_poll_now_and_the_cancellation_is_pushed()
    {
        await OnboardDemoUserWithPushDeviceAsync();
        await using var app = fixture.CreateWorkerWebApp();
        await app.StartAsync();
        using var worker = app.GetTestClient();

        // First nudge seeds the snapshots silently; wait for it to finish.
        await WaitForJobToSucceedAsync(await NudgeAsync(worker));
        await using var snapshotDb = fixture.CreateDbContext();
        var lesson = await snapshotDb.LessonSnapshots.OrderBy(s => s.StartTs).FirstAsync();

        using (var mock = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) })
        {
            (await mock.PostAsync($"/mock/cancel-lesson/{lesson.UntisLessonId}", null)).EnsureSuccessStatusCode();
        }

        await WaitForJobToSucceedAsync(await NudgeAsync(worker));

        var payload = await WaitForAsync(() => Task.FromResult(fixture.PushSender.Sent.FirstOrDefault()));
        Assert.Equal($"{lesson.Subject} has been cancelled", payload.Body);
    }

    [Fact]
    public async Task Health_endpoint_is_up()
    {
        await using var app = fixture.CreateWorkerWebApp();
        await app.StartAsync();
        using var worker = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync(WorkerApp.HealthPath)).StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.18.0.1", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("::ffff:192.168.1.20", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("2606:4700::1111", false)]
    public void Dashboard_filter_allows_only_loopback_and_private_addresses(string address, bool allowed) =>
        Assert.Equal(allowed, LanDashboardAuthorizationFilter.IsLan(IPAddress.Parse(address)));

    private static async Task<string> NudgeAsync(HttpClient worker)
    {
        var response = await worker.PostAsync("/nudge", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = body.GetProperty("job_id").GetString();
        Assert.False(string.IsNullOrEmpty(jobId));
        return jobId;
    }

    private static async Task WaitForJobToSucceedAsync(string jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var state = JobStorage.Current.GetConnection().GetJobData(jobId)?.State;
            if (state == "Succeeded") return;
            Assert.NotEqual("Failed", state);
            await Task.Delay(250);
        }
        throw new TimeoutException($"nudged job {jobId} did not succeed in 30s");
    }

    private static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe) where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await probe() is { } found) return found;
            await Task.Delay(250);
        }
        throw new TimeoutException("the nudged poll did not complete in 30s");
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanResponseWire(
        string Token,
        [property: System.Text.Json.Serialization.JsonPropertyName("user_id")] Guid UserId);

    private async Task OnboardDemoUserWithPushDeviceAsync()
    {
        using var mock = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
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
    }
}
