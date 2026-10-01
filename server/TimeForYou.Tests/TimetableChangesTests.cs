using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>GET /timetable and GET /changes (issue #34).</summary>
[Collection(SeamACollection.Name)]
public sealed class TimetableChangesTests(SeamAFixture fixture) : IAsyncLifetime
{
    // Wednesday — so "this week" is Mon 2026-10-05 .. Sun 2026-10-11, "next week" ends Sun 2026-10-18.
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

    /// <summary>Seeds a user with one device and returns the user id and that device's bearer token.</summary>
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

    private async Task<LessonSnapshot> SeedLessonAsync(Guid userId, DateTimeOffset start)
    {
        await using var db = fixture.CreateDbContext();
        var lesson = new LessonSnapshot
        {
            UserId = userId,
            UntisLessonId = Guid.NewGuid().ToString("N"),
            Subject = "Maths",
            StartTs = start,
            EndTs = start.AddHours(1),
            Status = LessonStatus.Scheduled,
        };
        db.LessonSnapshots.Add(lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    private async Task<ChangeLogEntry> SeedChangeAsync(
        Guid userId, Guid lessonId, DateTimeOffset detectedAt, ChangeType type = ChangeType.Room)
    {
        await using var db = fixture.CreateDbContext();
        var change = new ChangeLogEntry
        {
            UserId = userId,
            LessonId = lessonId,
            ChangeType = type,
            OldValue = "A1",
            NewValue = "B2",
            DetectedAt = detectedAt,
        };
        db.ChangeLog.Add(change);
        await db.SaveChangesAsync();
        return change;
    }

    private static HttpClient Authed(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Timetable_returns_only_lessons_in_this_week_and_next()
    {
        var (userId, token) = await SeedUserAsync("a");
        var thisMonday = await SeedLessonAsync(userId, new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
        var nextSunday = await SeedLessonAsync(userId, new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero));
        await SeedLessonAsync(userId, new(2026, 10, 4, 20, 0, 0, TimeSpan.Zero)); // last week
        await SeedLessonAsync(userId, new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero)); // week after next

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var lessons = await client.GetFromJsonAsync<JsonElement[]>("/timetable");

        Assert.Equal(
            [thisMonday.Id, nextSunday.Id],
            lessons!.Select(l => l.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task Timetable_uses_the_openapi_field_names_and_enum_wire_values()
    {
        var (userId, token) = await SeedUserAsync("a");
        await using (var db = fixture.CreateDbContext())
        {
            db.LessonSnapshots.Add(new LessonSnapshot
            {
                UserId = userId,
                UntisLessonId = "L1",
                Subject = "Physics",
                StartTs = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero),
                EndTs = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero),
                Status = LessonStatus.Cancelled,
            });
            await db.SaveChangesAsync();
        }

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var lesson = (await client.GetFromJsonAsync<JsonElement[]>("/timetable"))!.Single();

        Assert.Equal(
            ["end_ts", "id", "lecturer", "room", "start_ts", "status", "subject", "untis_lesson_id"],
            lesson.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("cancelled", lesson.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, lesson.GetProperty("room").ValueKind);
        Assert.Equal("L1", lesson.GetProperty("untis_lesson_id").GetString());
    }

    [Fact]
    public async Task Timetable_never_returns_another_users_lessons()
    {
        var (userA, tokenA) = await SeedUserAsync("a");
        var (userB, _) = await SeedUserAsync("b");
        var mine = await SeedLessonAsync(userA, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        await SeedLessonAsync(userB, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));

        using var factory = CreateFactory();
        using var client = Authed(factory, tokenA);

        var lessons = await client.GetFromJsonAsync<JsonElement[]>("/timetable");

        Assert.Equal(mine.Id, lessons!.Single().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Changes_are_ordered_newest_first_and_use_the_openapi_shape()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        var oldest = await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-3));
        var newest = await SeedChangeAsync(userId, lesson.Id, Now.AddHours(-1), ChangeType.Cancelled);
        var middle = await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-1));

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var changes = await client.GetFromJsonAsync<JsonElement[]>("/changes");

        Assert.Equal(
            [newest.Id, middle.Id, oldest.Id],
            changes!.Select(c => c.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(
            ["change_type", "detected_at", "id", "lesson_id", "new_value", "old_value"],
            changes![0].EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("cancelled", changes[0].GetProperty("change_type").GetString());
        Assert.Equal(lesson.Id, changes[0].GetProperty("lesson_id").GetGuid());
    }

    [Fact]
    public async Task Changes_since_filters_inclusively()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-3));
        var boundary = await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-1));
        var recent = await SeedChangeAsync(userId, lesson.Id, Now.AddHours(-1));

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var since = Uri.EscapeDataString(boundary.DetectedAt.ToString("O"));
        var changes = await client.GetFromJsonAsync<JsonElement[]>($"/changes?since={since}");

        Assert.Equal(
            [recent.Id, boundary.Id],
            changes!.Select(c => c.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task Changes_window_defaults_to_and_is_capped_at_30_days()
    {
        var (userId, token) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-31));
        var inWindow = await SeedChangeAsync(userId, lesson.Id, Now.AddDays(-29));

        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var byDefault = await client.GetFromJsonAsync<JsonElement[]>("/changes");
        var farBack = Uri.EscapeDataString(Now.AddDays(-365).ToString("O"));
        var capped = await client.GetFromJsonAsync<JsonElement[]>($"/changes?since={farBack}");

        Assert.Equal(inWindow.Id, byDefault!.Single().GetProperty("id").GetGuid());
        Assert.Equal(inWindow.Id, capped!.Single().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Changes_never_returns_another_users_changes()
    {
        var (userA, tokenA) = await SeedUserAsync("a");
        var (userB, _) = await SeedUserAsync("b");
        var lessonA = await SeedLessonAsync(userA, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        var lessonB = await SeedLessonAsync(userB, new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        var mine = await SeedChangeAsync(userA, lessonA.Id, Now.AddHours(-2));
        await SeedChangeAsync(userB, lessonB.Id, Now.AddHours(-1));

        using var factory = CreateFactory();
        using var client = Authed(factory, tokenA);

        var changes = await client.GetFromJsonAsync<JsonElement[]>("/changes");

        Assert.Equal(mine.Id, changes!.Single().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Invalid_since_returns_400()
    {
        var (_, token) = await SeedUserAsync("a");
        using var factory = CreateFactory();
        using var client = Authed(factory, token);

        var response = await client.GetAsync("/changes?since=yesterday");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/timetable")]
    [InlineData("/changes")]
    public async Task Missing_token_returns_401(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
