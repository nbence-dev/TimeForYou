using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// The per-user "change alerts" setting (#110), Seam A: GET/PATCH /settings through the real API,
/// and the poll job against the real mock, with pushes captured by <see cref="CapturingPushSender"/>.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class ChangeAlertsSettingTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Alerts_are_on_by_default()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);

        Assert.True(await GetAlertsEnabledAsync(api, device.Token));
    }

    [Fact]
    public async Task Turning_alerts_off_on_one_device_turns_them_off_on_the_users_other_devices()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var phone = await ScanDemoQrAsync(api);
        var laptop = await ScanDemoQrAsync(api);

        var response = await SetAlertsEnabledAsync(api, phone.Token, false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await GetAlertsEnabledAsync(api, laptop.Token));
    }

    [Fact]
    public async Task Patch_without_a_value_returns_400_and_changes_nothing()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/settings") { Content = JsonContent.Create(new { }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", device.Token);
        var response = await api.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await GetAlertsEnabledAsync(api, device.Token));
    }

    [Fact]
    public async Task Without_a_bearer_token_returns_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();

        var response = await api.GetAsync("/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_alerts_off_a_mock_change_is_logged_but_not_pushed()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);
        await GivePushTokenAsync(device.UserId);
        await RunPollCycleAsync();
        var lesson = (await SnapshotsInOrderAsync())[0];

        (await SetAlertsEnabledAsync(api, device.Token, false)).EnsureSuccessStatusCode();
        await CancelInMockAsync(lesson.UntisLessonId);
        await RunPollCycleAsync();

        var entry = Assert.Single(await ChangeLogAsync());
        Assert.Equal(ChangeType.Cancelled, entry.ChangeType);
        Assert.Empty(fixture.PushSender.Sent);
    }

    [Fact]
    public async Task Turning_alerts_back_on_resumes_one_grouped_push()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);
        await GivePushTokenAsync(device.UserId);
        await RunPollCycleAsync();
        var lessons = await SnapshotsInOrderAsync();

        (await SetAlertsEnabledAsync(api, device.Token, false)).EnsureSuccessStatusCode();
        await CancelInMockAsync(lessons[0].UntisLessonId);
        await RunPollCycleAsync();
        Assert.Empty(fixture.PushSender.Sent);

        (await SetAlertsEnabledAsync(api, device.Token, true)).EnsureSuccessStatusCode();
        await ChangeRoomInMockAsync(lessons[1].UntisLessonId, "Z98");
        await ChangeRoomInMockAsync(lessons[2].UntisLessonId, "Z99");
        await RunPollCycleAsync();

        Assert.Equal(3, (await ChangeLogAsync()).Count);
        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal(2, payload.ChangeCount);
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record ScannedDevice(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("user_id")] Guid UserId);

    private sealed record SettingsWire(
        [property: JsonPropertyName("change_alerts_enabled")] bool ChangeAlertsEnabled);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    /// <summary>Links a new device for the mock's demo student through the real POST /qr-scan.
    /// Scanning again adds another device for the same user.</summary>
    private async Task<ScannedDevice> ScanDemoQrAsync(HttpClient api)
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScannedDevice>()
            ?? throw new InvalidOperationException("qr-scan returned no body");
    }

    private static async Task<bool> GetAlertsEnabledAsync(HttpClient api, string bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await api.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SettingsWire>()
            ?? throw new InvalidOperationException("settings returned no body");
        return body.ChangeAlertsEnabled;
    }

    private static async Task<HttpResponseMessage> SetAlertsEnabledAsync(HttpClient api, string bearer, bool enabled)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/settings")
        {
            Content = JsonContent.Create(new { change_alerts_enabled = enabled }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await api.SendAsync(request);
    }

    /// <summary>Gives the user's devices a push token directly (registering one is #36's job).</summary>
    private async Task GivePushTokenAsync(Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        foreach (var device in await db.Devices.Where(d => d.UserId == userId).ToListAsync())
        {
            device.PushToken = $"test-push-token-{device.Id}";
            device.Platform = Enum.GetValues<DevicePlatform>()[0];
        }

        await db.SaveChangesAsync();
    }

    /// <summary>A fresh worker host per cycle, as in PollJobTests: state carries over only through the database.</summary>
    private async Task RunPollCycleAsync()
    {
        using var host = fixture.CreateWorkerHost();
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
}