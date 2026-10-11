using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>POST /devices — store FCM registration token (issue #36), Seam A.</summary>
[Collection(SeamACollection.Name)]
public sealed class DeviceRegistrationTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_stores_platform_and_push_token_on_the_calling_device()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearer = await ScanDemoQrAsync(api);

        var response = await RegisterAsync(api, bearer, new { platform = "android_fcm", push_token = "tok-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadDeviceAsync(response);
        Assert.Equal("android_fcm", body.Platform);
        Assert.Equal("tok-1", body.PushToken);

        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync();
        Assert.Equal(body.Id, device.Id);
        Assert.Equal("tok-1", device.PushToken);
        Assert.Equal(DevicePlatform.AndroidFcm, device.Platform);
    }

    [Fact]
    public async Task Re_registering_replaces_the_token_without_adding_a_row()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearer = await ScanDemoQrAsync(api);

        await RegisterAsync(api, bearer, new { platform = "android_fcm", push_token = "tok-1" });
        var response = await RegisterAsync(api, bearer, new { platform = "android_fcm", push_token = "tok-2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        var device = await db.Devices.SingleAsync();
        Assert.Equal("tok-2", device.PushToken);
    }

    [Fact]
    public async Task Two_devices_of_the_same_user_each_keep_their_own_token()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearerA = await ScanDemoQrAsync(api);
        var bearerB = await ScanDemoQrAsync(api);

        var deviceA = await ReadDeviceAsync(await RegisterAsync(api, bearerA, new { platform = "android_fcm", push_token = "token-a" }));
        var deviceB = await ReadDeviceAsync(await RegisterAsync(api, bearerB, new { platform = "web_push", push_token = "token-b" }));

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal("token-a", (await db.Devices.SingleAsync(d => d.Id == deviceA.Id)).PushToken);
        Assert.Equal("token-b", (await db.Devices.SingleAsync(d => d.Id == deviceB.Id)).PushToken);
    }

    [Fact]
    public async Task Registering_a_token_already_held_by_another_device_moves_it()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var oldBearer = await ScanDemoQrAsync(api);
        var newBearer = await ScanDemoQrAsync(api); // reinstall: new device row, same phone

        var oldDevice = await ReadDeviceAsync(await RegisterAsync(api, oldBearer, new { platform = "android_fcm", push_token = "same-phone" }));
        var newDevice = await ReadDeviceAsync(await RegisterAsync(api, newBearer, new { platform = "android_fcm", push_token = "same-phone" }));

        await using var db = fixture.CreateDbContext();
        Assert.Null((await db.Devices.SingleAsync(d => d.Id == oldDevice.Id)).PushToken);
        Assert.Equal("same-phone", (await db.Devices.SingleAsync(d => d.Id == newDevice.Id)).PushToken);
    }

    [Fact]
    public async Task Unknown_platform_returns_400()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearer = await ScanDemoQrAsync(api);

        var response = await RegisterAsync(api, bearer, new { platform = "ios_apns", push_token = "tok-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_push_token_returns_400()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearer = await ScanDemoQrAsync(api);

        var response = await RegisterAsync(api, bearer, new { platform = "android_fcm" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_bearer_token_returns_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();

        var response = await api.PostAsJsonAsync("/devices", new { platform = "android_fcm", push_token = "tok-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Poll_job_pushes_only_to_the_users_devices_that_registered_a_token()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var registeredBearer = await ScanDemoQrAsync(api);
        await ScanDemoQrAsync(api); // a second device for the same user that never registers a token
        var registered = await ReadDeviceAsync(
            await RegisterAsync(api, registeredBearer, new { platform = "web_push", push_token = "only-this-one" }));

        await RunPollCycleAsync(); // first poll seeds silently

        string lessonId;
        await using (var db = fixture.CreateDbContext())
        {
            lessonId = (await db.LessonSnapshots.OrderBy(s => s.StartTs).FirstAsync()).UntisLessonId;
        }

        using (var mock = CreateMockClient())
        {
            (await mock.PostAsync($"/mock/cancel-lesson/{lessonId}", null)).EnsureSuccessStatusCode();
        }

        await RunPollCycleAsync();

        var payload = Assert.Single(fixture.PushSender.Sent);
        Assert.Equal(registered.Id, payload.DeviceId);
        Assert.Equal("only-this-one", payload.PushToken);
        Assert.Equal(DevicePlatform.WebPush, payload.Platform);
    }

    [Fact]
    public async Task Unlink_removes_only_the_calling_device_and_its_token_stops_working()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var unlinked = await ScanDemoQrAsync(api);
        var kept = await ScanDemoQrAsync(api);

        var response = await SendAsync(api, HttpMethod.Delete, "/devices", unlinked);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(api, HttpMethod.Get, "/timetable", unlinked)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(api, HttpMethod.Get, "/timetable", kept)).StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Devices.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Unlink_without_a_bearer_token_returns_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();

        var response = await api.DeleteAsync("/devices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unlinked_device_is_not_pushed_to()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var bearer = await ScanDemoQrAsync(api);
        var keptBearer = await ScanDemoQrAsync(api);
        await RegisterAsync(api, bearer, new { platform = "web_push", push_token = "gone" });
        await RegisterAsync(api, keptBearer, new { platform = "web_push", push_token = "kept" });
        await RunPollCycleAsync(); // first poll seeds silently
        await SendAsync(api, HttpMethod.Delete, "/devices", bearer);

        string lessonId;
        await using (var db = fixture.CreateDbContext())
        {
            lessonId = (await db.LessonSnapshots.OrderBy(s => s.StartTs).FirstAsync()).UntisLessonId;
        }

        using (var mock = CreateMockClient())
        {
            (await mock.PostAsync($"/mock/cancel-lesson/{lessonId}", null)).EnsureSuccessStatusCode();
        }

        await RunPollCycleAsync();

        // The kept device proves the cancel did produce a push; only the unlinked one is missing.
        Assert.Equal("kept", Assert.Single(fixture.PushSender.Sent).PushToken);
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanResponseWire(string Token);

    private sealed record DeviceWire(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("platform")] string Platform,
        [property: JsonPropertyName("push_token")] string PushToken);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

    /// <summary>Scans the mock's demo QR through the real POST /qr-scan; returns the new device's bearer token.</summary>
    private async Task<string> ScanDemoQrAsync(HttpClient api)
    {
        using var mock = CreateMockClient();
        var payload = await mock.GetFromJsonAsync<QrPayload>("/qr-payload")
            ?? throw new InvalidOperationException("mock returned no QR payload");

        var response = await api.PostAsJsonAsync("/qr-scan", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<QrScanResponseWire>()
            ?? throw new InvalidOperationException("qr-scan returned no body");
        return body.Token;
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient api, string bearer, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/devices") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await api.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient api, HttpMethod method, string path, string bearer)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await api.SendAsync(request);
    }

    private static async Task<DeviceWire> ReadDeviceAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceWire>()
            ?? throw new InvalidOperationException("POST /devices returned no body");
    }

    private async Task RunPollCycleAsync()
    {
        using var host = fixture.CreateWorkerHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }
}