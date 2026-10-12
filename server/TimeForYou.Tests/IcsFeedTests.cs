using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ical.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>GET /ics/{token}.ics and GET /calendar (issue #37).</summary>
[Collection(SeamACollection.Name)]
public sealed class IcsFeedTests(SeamAFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        fixture.CreateApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))));

    // Mirrors AuthCrypto.HashBearerToken (internal to the API): devices store only SHA-256 hex of the token.
    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<(Guid UserId, string Token)> SeedUserAsync(string name)
    {
        var token = $"token-{name}-{Guid.NewGuid():N}";
        await using var db = fixture.CreateDbContext();
        var user = new User
        {
            School = "test",
            CredentialsProtected = "opaque",
            UntisUserHash = $"hash-{name}-{Guid.NewGuid():N}",
        };
        db.Users.Add(user);
        db.Devices.Add(new Device { User = user, BearerTokenHash = HashToken(token) });
        await db.SaveChangesAsync();
        return (user.Id, token);
    }

    private async Task<LessonSnapshot> SeedLessonAsync(
        Guid userId,
        DateTimeOffset start,
        string untisLessonId = "L1",
        string? room = "A1",
        LessonStatus status = LessonStatus.Scheduled)
    {
        await using var db = fixture.CreateDbContext();
        var lesson = new LessonSnapshot
        {
            UserId = userId,
            UntisLessonId = untisLessonId,
            Subject = "Maths",
            Lecturer = "Dr Smith",
            Room = room,
            StartTs = start,
            EndTs = start.AddHours(1),
            Status = status,
            // Pin to the test clock; otherwise the DB default now() leaks wall-clock time into LAST-MODIFIED.
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        db.LessonSnapshots.Add(lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    private static HttpClient Authed(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Asks the API for the user's feed URL, as the app would, and returns its path.</summary>
    private static async Task<string> GetFeedPathAsync(HttpClient authed)
    {
        var body = await authed.GetFromJsonAsync<JsonElement>("/calendar");
        return new Uri(body.GetProperty("url").GetString()!).AbsolutePath;
    }

    private static Calendar ParseCalendar(string ics) => Calendar.Load(ics)!;

    [Fact]
    public async Task Reset_gives_a_new_url_the_old_one_is_404_and_the_new_one_serves_the_feed()
    {
        var (userId, token) = await SeedUserAsync("a");
        await SeedLessonAsync(userId, Now.AddDays(1));

        using var factory = CreateFactory();
        using var authed = Authed(factory, token);
        using var anonymous = factory.CreateClient();
        var oldPath = await GetFeedPathAsync(authed);

        var reset = await authed.PostAsync("/calendar/reset", null);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newPath = new Uri((await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!).AbsolutePath;
        Assert.NotEqual(oldPath, newPath);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(oldPath)).StatusCode);
        var feed = await anonymous.GetAsync(newPath);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.Single(ParseCalendar(await feed.Content.ReadAsStringAsync()).Events);
        Assert.Equal(newPath, await GetFeedPathAsync(authed));
    }

    [Fact]
    public async Task Reset_before_the_feed_was_ever_requested_still_creates_one()
    {
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory();
        using var authed = Authed(factory, token);
        var reset = await authed.PostAsync("/calendar/reset", null);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newPath = new Uri((await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!).AbsolutePath;
        Assert.Equal(newPath, await GetFeedPathAsync(authed));
    }

    [Fact]
    public async Task Reset_for_one_user_leaves_another_users_feed_alone()
    {
        var (_, tokenA) = await SeedUserAsync("a");
        var (_, tokenB) = await SeedUserAsync("b");

        using var factory = CreateFactory();
        using var a = Authed(factory, tokenA);
        using var b = Authed(factory, tokenB);
        var bPath = await GetFeedPathAsync(b);

        await a.PostAsync("/calendar/reset", null);

        Assert.Equal(bPath, await GetFeedPathAsync(b));
    }

    [Fact]
    public async Task Reset_without_a_bearer_token_returns_401()
    {
        using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsync("/calendar/reset", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_url_leads_to_a_valid_text_calendar_feed()
    {
        var (userId, token) = await SeedUserAsync("a");
        await SeedLessonAsync(userId, Now.AddDays(1));

        using var factory = CreateFactory();
        using var authed = Authed(factory, token);
        var feedPath = await GetFeedPathAsync(authed);

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync(feedPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        var calendar = ParseCalendar(await response.Content.ReadAsStringAsync());
        Assert.Single(calendar.Events);
    }

    private async Task<string> FetchFeedAsync(WebApplicationFactory<Program> factory, string bearerToken)
    {
        using var authed = Authed(factory, bearerToken);
        var feedPath = await GetFeedPathAsync(authed);
        using var anonymous = factory.CreateClient();
        return await anonymous.GetStringAsync(feedPath);
    }

    [Fact]
    public async Task Feed_contains_only_lessons_that_start_in_the_future()
    {
        var (userId, token) = await SeedUserAsync("a");
        await SeedLessonAsync(userId, Now.AddDays(-1), "past");
        await SeedLessonAsync(userId, Now, "starting-right-now");
        await SeedLessonAsync(userId, Now.AddMinutes(1), "soon");
        await SeedLessonAsync(userId, Now.AddDays(3), "later");

        using var factory = CreateFactory();
        var calendar = ParseCalendar(await FetchFeedAsync(factory, token));

        Assert.Equal(
            ["soon@timeforyou", "later@timeforyou"],
            calendar.Events.Select(e => e.Uid!).ToArray());
    }

    [Fact]
    public async Task Feed_never_contains_another_users_lessons()
    {
        var (userA, tokenA) = await SeedUserAsync("a");
        var (userB, _) = await SeedUserAsync("b");
        await SeedLessonAsync(userA, Now.AddDays(1), "mine");
        await SeedLessonAsync(userB, Now.AddDays(1), "theirs");

        using var factory = CreateFactory();
        var calendar = ParseCalendar(await FetchFeedAsync(factory, tokenA));

        Assert.Equal("mine@timeforyou", calendar.Events.Single().Uid);
    }

    [Fact]
    public async Task Feed_event_keeps_its_uid_and_shows_the_new_room_after_a_room_change()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, Now.AddDays(1), "L42", room: "A1");

        using var factory = CreateFactory();
        var before = ParseCalendar(await FetchFeedAsync(factory, token)).Events.Single();

        await using (var db = fixture.CreateDbContext())
        {
            var tracked = await db.LessonSnapshots.FindAsync(lesson.Id);
            tracked!.Room = "B7";
            await db.SaveChangesAsync();
        }

        var after = ParseCalendar(await FetchFeedAsync(factory, token)).Events.Single();

        Assert.Equal("A1", before.Location);
        Assert.Equal("B7", after.Location);
        Assert.Equal(before.Uid, after.Uid);
    }

    [Fact]
    public async Task Feed_marks_a_cancelled_lesson_as_cancelled_and_keeps_it()
    {
        var (userId, token) = await SeedUserAsync("a");
        await SeedLessonAsync(userId, Now.AddDays(1), "ok");
        await SeedLessonAsync(userId, Now.AddDays(2), "gone", status: LessonStatus.Cancelled);

        using var factory = CreateFactory();
        var ics = await FetchFeedAsync(factory, token);
        var events = ParseCalendar(ics).Events.ToDictionary(e => e.Uid!);

        Assert.Equal("CANCELLED", events["gone@timeforyou"].Status);
        Assert.Equal("CONFIRMED", events["ok@timeforyou"].Status);
        Assert.Contains("STATUS:CANCELLED", ics);
    }

    [Fact]
    public async Task Feed_event_description_carries_the_lessons_notes()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, Now.AddDays(1));
        await using (var db = fixture.CreateDbContext())
        {
            db.Notes.Add(new Note { UserId = userId, LessonId = lesson.Id, Body = "Bring the lab coat" });
            await db.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        var ev = ParseCalendar(await FetchFeedAsync(factory, token)).Events.Single();

        Assert.Contains("Bring the lab coat", ev.Description);
    }

    [Fact]
    public async Task Feed_event_last_modified_moves_forward_when_a_note_is_edited()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, Now.AddDays(1));
        var noteEdited = Now.AddHours(5);
        await using (var db = fixture.CreateDbContext())
        {
            db.Notes.Add(new Note
            {
                UserId = userId,
                LessonId = lesson.Id,
                Body = "Bring the lab coat",
                UpdatedAt = noteEdited,
            });
            await db.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        var ev = ParseCalendar(await FetchFeedAsync(factory, token)).Events.Single();

        Assert.Equal(noteEdited.UtcDateTime, ev.LastModified!.AsUtc);
    }

    [Fact]
    public async Task Feed_declares_exactly_one_prodid_as_rfc_5545_requires()
    {
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory();
        var ics = await FetchFeedAsync(factory, token);

        Assert.Single(ics.Split("\r\n"), line => line.StartsWith("PRODID:"));
    }

    [Fact]
    public async Task Feed_for_a_user_with_no_future_lessons_is_an_empty_valid_calendar()
    {
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory();
        var calendar = ParseCalendar(await FetchFeedAsync(factory, token));

        Assert.Empty(calendar.Events);
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")] // right length, unknown
    [InlineData("short")]
    public async Task Unknown_token_returns_404(string token)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/ics/{token}.ics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Calendar_returns_the_same_url_on_every_call_and_from_every_device()
    {
        var (userId, firstDevice) = await SeedUserAsync("a");
        var secondDevice = $"second-device-{Guid.NewGuid():N}";
        await using (var db = fixture.CreateDbContext())
        {
            db.Devices.Add(new Device { UserId = userId, BearerTokenHash = HashToken(secondDevice) });
            await db.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var first = Authed(factory, firstDevice);
        using var second = Authed(factory, secondDevice);

        var urls = new[]
        {
            (await first.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString(),
            (await first.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString(),
            (await second.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString(),
        };

        Assert.Single(urls.Distinct());
    }

    [Fact]
    public async Task Calendar_concurrent_first_calls_still_agree_on_one_url()
    {
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var urls = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            (await client.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString()));

        Assert.Single(urls.Distinct());
    }

    [Fact]
    public async Task Calendar_gives_each_user_a_distinct_64_character_url_safe_token()
    {
        var (_, tokenA) = await SeedUserAsync("a");
        var (_, tokenB) = await SeedUserAsync("b");

        using var factory = CreateFactory();
        using var a = Authed(factory, tokenA);
        using var b = Authed(factory, tokenB);

        var pathA = await GetFeedPathAsync(a);
        var pathB = await GetFeedPathAsync(b);

        Assert.NotEqual(pathA, pathB);
        foreach (var path in new[] { pathA, pathB })
        {
            var feedToken = path["/ics/".Length..^".ics".Length];
            Assert.Equal(64, feedToken.Length);
            Assert.Matches("^[A-Za-z0-9_-]{64}$", feedToken);
        }
    }

    [Fact]
    public async Task Calendar_url_uses_the_configured_public_base_url()
    {
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("PUBLIC_BASE_URL", "https://tfy.example.org/"));
        using var client = Authed(factory, token);

        var url = (await client.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString();

        Assert.StartsWith("https://tfy.example.org/ics/", url);
        Assert.EndsWith(".ics", url);
    }

    [Fact]
    public async Task Calendar_url_falls_back_to_the_request_origin_when_the_public_base_url_is_blank()
    {
        // docker-compose passes PUBLIC_BASE_URL through as "" when .env leaves it unset.
        var (_, token) = await SeedUserAsync("a");

        using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("PUBLIC_BASE_URL", ""));
        using var client = Authed(factory, token);

        var url = (await client.GetFromJsonAsync<JsonElement>("/calendar")).GetProperty("url").GetString();

        Assert.StartsWith("http://localhost/ics/", url);
    }

    [Fact]
    public async Task Calendar_without_a_bearer_token_returns_401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/calendar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
