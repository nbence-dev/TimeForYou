using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>POST /qr-scan + bearer auth + encrypted credentials (issue #31).</summary>
[Collection(SeamACollection.Name)]
public sealed class QrScanAuthTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record QrPayload(string Server, string School, string User, string Key);

    private sealed record QrScanResponseWire(string Token, [property: JsonPropertyName("user_id")] Guid UserId);

    private async Task<QrPayload> FetchDemoQrPayloadAsync()
    {
        using var mockClient = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };
        var payload = await mockClient.GetFromJsonAsync<QrPayload>("/qr-payload");
        return payload ?? throw new InvalidOperationException("mock returned no QR payload");
    }

    [Fact]
    public async Task First_scan_creates_one_user_and_one_device()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/qr-scan", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QrScanResponseWire>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Devices.CountAsync());
    }

    [Fact]
    public async Task Second_scan_of_the_same_qr_reuses_the_same_user_with_a_new_device_and_token()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var first = await (await client.PostAsJsonAsync("/qr-scan", payload)).Content.ReadFromJsonAsync<QrScanResponseWire>();
        var second = await (await client.PostAsJsonAsync("/qr-scan", payload)).Content.ReadFromJsonAsync<QrScanResponseWire>();

        Assert.Equal(first!.UserId, second!.UserId);
        Assert.NotEqual(first.Token, second.Token);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(2, await db.Devices.CountAsync());
    }

    [Fact]
    public async Task Concurrent_scans_of_the_same_qr_still_collapse_onto_one_user()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var responses = await Task.WhenAll(
            clientA.PostAsJsonAsync("/qr-scan", payload),
            clientB.PostAsJsonAsync("/qr-scan", payload));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<QrScanResponseWire>()));

        Assert.Equal(bodies[0]!.UserId, bodies[1]!.UserId);
        Assert.NotEqual(bodies[0]!.Token, bodies[1]!.Token);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(2, await db.Devices.CountAsync());
    }

    [Fact]
    public async Task Malformed_body_returns_400()
    {
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/qr-scan", new { school = "eduvos" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_key_is_rejected_with_400()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/qr-scan", payload with { Key = payload.Key + "-wrong" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_table_stores_no_plaintext_webuntis_credentials()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/qr-scan", payload);

        await using var db = fixture.CreateDbContext();
        var user = await db.Users.SingleAsync();

        Assert.DoesNotContain(payload.Key, user.CredentialsProtected, StringComparison.Ordinal);
        Assert.DoesNotContain(payload.User, user.CredentialsProtected, StringComparison.Ordinal);
        Assert.DoesNotContain(payload.Server, user.CredentialsProtected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_missing_token_and_accepts_a_valid_one()
    {
        var payload = await FetchDemoQrPayloadAsync();
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var unauthenticated = await client.GetAsync("/timetable");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var scan = await (await client.PostAsJsonAsync("/qr-scan", payload)).Content.ReadFromJsonAsync<QrScanResponseWire>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scan!.Token);

        var authenticated = await client.GetAsync("/timetable");
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    [Fact]
    public async Task Unknown_bearer_token_is_rejected()
    {
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/timetable");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
