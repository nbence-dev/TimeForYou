using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Api.Features.Auth;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.WebUntis;

namespace TimeForYou.Tests;

/// <summary>
/// Poll on first registration (#58), Seam A: a QR scan that creates a user enqueues a poll for that
/// user only, the poll fills GET /timetable without a push, and a failed enqueue never fails the scan.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class FirstVisitPollTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_new_users_scan_enqueues_a_poll_that_fills_their_timetable_without_a_push()
    {
        var enqueuer = new RecordingFirstPollEnqueuer();
        using var factory = CreateApiFactory(enqueuer);
        using var api = factory.CreateClient();

        var scan = await ScanDemoQrAsync(api);
        var queuedUserId = Assert.Single(enqueuer.UserIds);
        Assert.Equal(scan.UserId, queuedUserId);
        await GivePushTokenAsync(scan.UserId); // so a push, if one were sent, would be captured

        await RunTriggeredPollAsync(queuedUserId);

        Assert.NotEmpty(await GetTimetableAsync(api, scan.Token));
        Assert.Empty(fixture.PushSender.Sent);
    }

    [Fact]
    public async Task A_second_device_for_a_known_user_enqueues_nothing()
    {
        var enqueuer = new RecordingFirstPollEnqueuer();
        using var factory = CreateApiFactory(enqueuer);
        using var api = factory.CreateClient();

        var first = await ScanDemoQrAsync(api);
        var second = await ScanDemoQrAsync(api);

        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(first.UserId, Assert.Single(enqueuer.UserIds));
    }

    [Fact]
    public async Task A_failed_enqueue_still_returns_the_token_and_user_id()
    {
        using var factory = CreateApiFactory(new ThrowingFirstPollEnqueuer());
        using var api = factory.CreateClient();

        var response = await api.PostAsJsonAsync("/qr-scan", await FetchQrPayloadAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrScanResponseWire>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.NotEqual(Guid.Empty, body.UserId);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task The_triggered_poll_polls_only_the_new_user()
    {
        // Another user a full poll cycle would also fetch (same working demo credentials, separate identity).
        var payload = await FetchQrPayloadAsync();
        Guid otherUserId;
        using (var host = fixture.CreateWorkerHost())
        {
            var protector = host.Services.GetRequiredService<WebUntisCredentialProtector>();
            await using var db = fixture.CreateDbContext();
            var other = new User
            {
                School = payload.School,
                CredentialsProtected = protector.Protect(
                    new WebUntisCredentials(payload.Server, payload.School, payload.User, payload.Key)),
                UntisUserHash = "some-other-user",
            };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            otherUserId = other.Id;
        }

        var enqueuer = new RecordingFirstPollEnqueuer();
        using var factory = CreateApiFactory(enqueuer);
        using var api = factory.CreateClient();
        var scan = await ScanDemoQrAsync(api);

        await RunTriggeredPollAsync(Assert.Single(enqueuer.UserIds));

        await using var check = fixture.CreateDbContext();
        Assert.True(await check.LessonSnapshots.AnyAsync(s => s.UserId == scan.UserId));
        Assert.False(await check.LessonSnapshots.AnyAsync(s => s.UserId == otherUserId));
    }

    [Fact]
    public async Task The_real_enqueuer_puts_a_single_user_poll_in_the_shared_hangfire_queue()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();

        var scan = await ScanDemoQrAsync(api);

        var storage = factory.Services.GetRequiredService<JobStorage>();
        var queued = storage.GetMonitoringApi().EnqueuedJobs("default", 0, 100)
            .Select(entry => entry.Value.Job)
            .Single(job => job is not null && job.Args.Count > 0 && Equals(job.Args[0], scan.UserId));
        Assert.Equal(typeof(IUserPollJob), queued!.Type);
        Assert.Equal(nameof(IUserPollJob.RunForUserAsync), queued.Method.Name);
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanResponseWire(
        string Token,
        [property: JsonPropertyName("user_id")] Guid UserId);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    private WebApplicationFactory<Program> CreateApiFactory(IFirstPollEnqueuer enqueuer) =>
        fixture.CreateApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(enqueuer)));

    private async Task<QrPayload> FetchQrPayloadAsync()
    {
        using var mock = CreateMockClient();
        return await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");
    }

    private async Task<QrScanResponseWire> ScanDemoQrAsync(HttpClient api)
    {
        var response = await api.PostAsJsonAsync("/qr-scan", await FetchQrPayloadAsync());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QrScanResponseWire>()
            ?? throw new InvalidOperationException("qr-scan returned no body");
    }

    private async Task GivePushTokenAsync(Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync(d => d.UserId == userId);
        device.PushToken = "test-push-token";
        device.Platform = DevicePlatform.AndroidFcm;
        await db.SaveChangesAsync();
    }

    /// <summary>Runs exactly what the worker's Hangfire would run for the enqueued job.</summary>
    private async Task RunTriggeredPollAsync(Guid userId)
    {
        using var host = fixture.CreateWorkerHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserPollJob>().RunForUserAsync(userId, CancellationToken.None);
    }

    private static async Task<List<JsonElement>> GetTimetableAsync(HttpClient api, string bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/timetable");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await api.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
    }

    private sealed class RecordingFirstPollEnqueuer : IFirstPollEnqueuer
    {
        private readonly ConcurrentQueue<Guid> _userIds = new();

        public IReadOnlyCollection<Guid> UserIds => _userIds;

        public void Enqueue(Guid userId) => _userIds.Enqueue(userId);
    }

    private sealed class ThrowingFirstPollEnqueuer : IFirstPollEnqueuer
    {
        public void Enqueue(Guid userId) => throw new InvalidOperationException("Hangfire storage unreachable");
    }
}
