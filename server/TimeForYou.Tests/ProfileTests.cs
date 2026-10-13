using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Tests.Infrastructure;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Tests;

/// <summary>
/// GET /profile (#113), Seam A: the student's name and class come from the real mock through a
/// poll, are saved on the user, and are read back through the real API.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class ProfileTests(SeamAFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        using var mock = CreateMockClient();
        (await mock.PostAsync("/mock/reset", null)).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Before_the_first_poll_the_profile_is_empty()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);

        var profile = await GetProfileAsync(api, device.Token);

        Assert.Null(profile.DisplayName);
        Assert.Null(profile.ClassName);
    }

    [Fact]
    public async Task After_a_poll_the_profile_has_the_students_name_and_class()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var device = await ScanDemoQrAsync(api);

        await RunPollCycleAsync();
        var profile = await GetProfileAsync(api, device.Token);

        Assert.Equal("Demo Student", profile.DisplayName);
        Assert.Equal("ITMDA3-B34", profile.ClassName);
    }

    [Fact]
    public async Task Every_device_of_the_student_sees_the_same_profile()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();
        var phone = await ScanDemoQrAsync(api);
        await RunPollCycleAsync();

        var laptop = await ScanDemoQrAsync(api);
        var profile = await GetProfileAsync(api, laptop.Token);

        Assert.Equal("Demo Student", profile.DisplayName);
        Assert.Equal(await GetProfileAsync(api, phone.Token), profile);
    }

    [Fact]
    public async Task Without_a_bearer_token_returns_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var api = factory.CreateClient();

        var response = await api.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record ScannedDevice(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("user_id")] Guid UserId);

    private sealed record ProfileWire(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("class_name")] string? ClassName);

    private HttpClient CreateMockClient() => new() { BaseAddress = new Uri(fixture.MockBaseUrl) };

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

    private static async Task<ProfileWire> GetProfileAsync(HttpClient api, string bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await api.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileWire>()
            ?? throw new InvalidOperationException("profile returned no body");
    }

    private async Task RunPollCycleAsync()
    {
        using var host = fixture.CreateWorkerHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PollJob>().RunAsync(CancellationToken.None);
    }
}