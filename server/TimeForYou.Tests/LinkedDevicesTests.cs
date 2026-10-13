using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>GET /devices: the caller's own devices (issue #111), Seam A.</summary>
[Collection(SeamACollection.Name)]
public sealed class LinkedDevicesTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Seeds a user with one device per entry (platform and push token as given); returns the user id, one bearer token and device id per device.</summary>
    private async Task<(Guid UserId, string[] Tokens, Guid[] DeviceIds)> SeedUserAsync(
        string name,
        params (DevicePlatform? Platform, string? PushToken)[] devices)
    {
        var tokens = devices.Select((_, i) => $"token-{name}-{i}-{Guid.NewGuid():N}").ToArray();
        await using var db = fixture.CreateDbContext();
        var user = new User
        {
            School = "test",
            CredentialsProtected = "opaque",
            UntisUserHash = $"hash-{name}-{Guid.NewGuid():N}",
        };
        db.Users.Add(user);
        var rows = devices
            .Select((d, i) => new Device
            {
                User = user,
                BearerTokenHash = HashToken(tokens[i]),
                Platform = d.Platform,
                PushToken = d.PushToken,
            })
            .ToArray();
        db.Devices.AddRange(rows);
        await db.SaveChangesAsync();
        return (user.Id, tokens, rows.Select(r => r.Id).ToArray());
    }

    private static HttpClient Authed(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Lists_both_devices_of_the_user_and_marks_the_one_asking()
    {
        var (_, tokens, ids) = await SeedUserAsync(
            "a",
            (DevicePlatform.WebPush, "push-web"),
            (DevicePlatform.AndroidFcm, "push-android"));

        using var factory = fixture.CreateApiFactory();
        using var asFirst = Authed(factory, tokens[0]);
        var list = await asFirst.GetFromJsonAsync<JsonElement>("/devices");

        Assert.Equal(2, list.GetArrayLength());
        var current = list.EnumerateArray().Single(d => d.GetProperty("is_current").GetBoolean());
        Assert.Equal(ids[0], current.GetProperty("id").GetGuid());
        Assert.Equal(ids.OrderBy(i => i), list.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).OrderBy(i => i));

        using var asSecond = Authed(factory, tokens[1]);
        var fromSecond = await asSecond.GetFromJsonAsync<JsonElement>("/devices");
        var currentFromSecond = fromSecond.EnumerateArray().Single(d => d.GetProperty("is_current").GetBoolean());
        Assert.Equal(ids[1], currentFromSecond.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Lists_this_device_first_then_the_others_most_recently_active_first()
    {
        var (_, tokens, ids) = await SeedUserAsync(
            "a",
            (DevicePlatform.WebPush, "p0"),
            (DevicePlatform.WebPush, "p1"),
            (DevicePlatform.AndroidFcm, "p2"));
        await using (var db = fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.Devices.Single(d => d.Id == ids[1]).LastSeenAt = now.AddHours(-5);
            db.Devices.Single(d => d.Id == ids[2]).LastSeenAt = now.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using var factory = fixture.CreateApiFactory();
        using var authed = Authed(factory, tokens[0]);
        var list = await authed.GetFromJsonAsync<JsonElement>("/devices");

        Assert.Equal(
            new[] { ids[0], ids[2], ids[1] },
            list.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task Never_lists_another_users_devices()
    {
        var (_, tokensA, idsA) = await SeedUserAsync("a", (DevicePlatform.WebPush, "push-a"));
        var (_, _, idsB) = await SeedUserAsync("b", (DevicePlatform.AndroidFcm, "push-b"), (DevicePlatform.WebPush, "push-b2"));

        using var factory = fixture.CreateApiFactory();
        using var a = Authed(factory, tokensA[0]);
        var list = await a.GetFromJsonAsync<JsonElement>("/devices");

        var listed = list.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(idsA, listed);
        Assert.DoesNotContain(listed, id => idsB.Contains(id));
    }

    [Fact]
    public async Task Shows_platform_and_whether_notifications_are_on_without_exposing_the_push_token()
    {
        var (_, tokens, ids) = await SeedUserAsync(
            "a",
            (DevicePlatform.WebPush, "secret-push-token"),
            (null, null)); // linked by QR scan, never registered a push token

        using var factory = fixture.CreateApiFactory();
        using var authed = Authed(factory, tokens[0]);
        var response = await authed.GetAsync("/devices");
        var raw = await response.Content.ReadAsStringAsync();
        var list = JsonDocument.Parse(raw).RootElement;

        var registered = list.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == ids[0]);
        Assert.Equal("web_push", registered.GetProperty("platform").GetString());
        Assert.True(registered.GetProperty("notifications_on").GetBoolean());

        var unregistered = list.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == ids[1]);
        Assert.Equal(JsonValueKind.Null, unregistered.GetProperty("platform").ValueKind);
        Assert.False(unregistered.GetProperty("notifications_on").GetBoolean());

        Assert.DoesNotContain("secret-push-token", raw);
        Assert.DoesNotContain("push_token", raw);
    }

    [Fact]
    public async Task A_device_that_was_unlinked_disappears_from_the_list()
    {
        var (_, tokens, _) = await SeedUserAsync("a", (DevicePlatform.WebPush, "p1"), (DevicePlatform.AndroidFcm, "p2"));

        using var factory = fixture.CreateApiFactory();
        using var first = Authed(factory, tokens[0]);
        using var second = Authed(factory, tokens[1]);
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync("/devices")).StatusCode);

        var list = await second.GetFromJsonAsync<JsonElement>("/devices");

        Assert.Equal(1, list.GetArrayLength());
        Assert.True(list[0].GetProperty("is_current").GetBoolean());
    }

    [Fact]
    public async Task Without_a_bearer_token_returns_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/devices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
