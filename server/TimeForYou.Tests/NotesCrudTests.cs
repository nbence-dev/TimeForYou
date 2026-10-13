using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>Notes CRUD: /lessons/{id}/notes and /notes/{id} (issue #35).</summary>
[Collection(SeamACollection.Name)]
public sealed class NotesCrudTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Seeds a user with the given number of devices; returns the user id and one bearer token per device.</summary>
    private async Task<(Guid UserId, string[] Tokens)> SeedUserAsync(string name, int devices = 1)
    {
        var tokens = Enumerable.Range(0, devices).Select(i => $"token-{name}-{i}-{Guid.NewGuid():N}").ToArray();
        await using var db = fixture.CreateDbContext();
        var user = new User
        {
            School = "test",
            CredentialsProtected = "opaque",
            UntisUserHash = $"hash-{name}-{Guid.NewGuid():N}",
        };
        db.Users.Add(user);
        foreach (var token in tokens)
        {
            db.Devices.Add(new Device { User = user, BearerTokenHash = HashToken(token) });
        }

        await db.SaveChangesAsync();
        return (user.Id, tokens);
    }

    private async Task<LessonSnapshot> SeedLessonAsync(Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        var start = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
        var lesson = new LessonSnapshot
        {
            UserId = userId,
            UntisLessonId = Guid.NewGuid().ToString("N"),
            Subject = "Maths",
            Room = "A1",
            StartTs = start,
            EndTs = start.AddHours(1),
            Status = LessonStatus.Scheduled,
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

    private static object Body(string body) => new { body };

    [Fact]
    public async Task Crud_round_trip_matches_the_openapi_status_codes_and_field_names()
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);

        var created = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("bring calculator"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = await created.Content.ReadFromJsonAsync<JsonElement>();
        var noteId = note.GetProperty("id").GetGuid();
        Assert.Equal(lesson.Id, note.GetProperty("lesson_id").GetGuid());
        Assert.Equal("bring calculator", note.GetProperty("body").GetString());
        Assert.True(note.TryGetProperty("created_at", out _));
        Assert.True(note.TryGetProperty("updated_at", out _));

        var listed = await client.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal([noteId], listed!.Select(n => n.GetProperty("id").GetGuid()).ToArray());

        var patched = await client.PatchAsJsonAsync($"/notes/{noteId}", Body("bring two calculators"));
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var updated = await patched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("bring two calculators", updated.GetProperty("body").GetString());
        Assert.True(updated.GetProperty("updated_at").GetDateTimeOffset() >= note.GetProperty("created_at").GetDateTimeOffset());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/notes/{noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/notes/{noteId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes"))!);
    }

    [Fact]
    public async Task Note_written_on_device_A_is_readable_and_editable_on_device_B_of_the_same_user()
    {
        var (userId, tokens) = await SeedUserAsync("a", devices: 2);
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var deviceA = Authed(factory, tokens[0]);
        using var deviceB = Authed(factory, tokens[1]);

        var created = await deviceA.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("from A"));
        var noteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var seenByB = await deviceB.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal("from A", Assert.Single(seenByB!).GetProperty("body").GetString());

        Assert.Equal(HttpStatusCode.OK, (await deviceB.PatchAsJsonAsync($"/notes/{noteId}", Body("edited by B"))).StatusCode);
        var seenByA = await deviceA.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal("edited by B", Assert.Single(seenByA!).GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_body_is_400_on_create_and_update(string body)
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);

        var create = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body(body));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.True((await create.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("message", out _));

        var good = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("ok"));
        var noteId = (await good.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/notes/{noteId}", Body(body))).StatusCode);
    }

    [Fact]
    public async Task Missing_body_property_is_400()
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);

        var response = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Body_of_500_characters_is_accepted_and_501_is_400()
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);

        var ok = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body(new string('x', 500)));
        var noteId = (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body(new string('x', 501)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/notes/{noteId}", Body(new string('x', 501)))).StatusCode);
    }

    [Fact]
    public async Task Another_users_lesson_and_note_are_404_on_every_route()
    {
        var (ownerId, ownerTokens) = await SeedUserAsync("owner");
        var (_, intruderTokens) = await SeedUserAsync("intruder");
        var lesson = await SeedLessonAsync(ownerId);
        using var factory = fixture.CreateApiFactory();
        using var owner = Authed(factory, ownerTokens[0]);
        using var intruder = Authed(factory, intruderTokens[0]);
        var created = await owner.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("private"));
        var noteId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/lessons/{lesson.Id}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PatchAsJsonAsync($"/notes/{noteId}", Body("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/notes/{noteId}")).StatusCode);

        var stillThere = await owner.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal("private", Assert.Single(stillThere!).GetProperty("body").GetString());
    }

    [Fact]
    public async Task Unknown_lesson_and_note_are_404()
    {
        var (_, tokens) = await SeedUserAsync("a");
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/lessons/{Guid.NewGuid()}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/notes/{Guid.NewGuid()}", Body("x"))).StatusCode);
    }

    [Fact]
    public async Task Requests_without_a_bearer_token_are_401()
    {
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/lessons/{Guid.NewGuid()}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/notes/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Note_stays_on_the_lesson_when_the_snapshot_room_and_time_change()
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);
        await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", Body("sticky"));

        // What the poll's upsert does to an existing snapshot row: update in place, same id.
        await using (var db = fixture.CreateDbContext())
        {
            var row = await db.LessonSnapshots.SingleAsync(l => l.Id == lesson.Id);
            row.Room = "B2";
            row.StartTs = row.StartTs.AddHours(2);
            row.EndTs = row.EndTs.AddHours(2);
            await db.SaveChangesAsync();
        }

        var notes = await client.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal("sticky", Assert.Single(notes!).GetProperty("body").GetString());
    }

        [Fact]
    public async Task Creating_twice_with_the_same_client_id_keeps_one_note()
    {
        var (userId, tokens) = await SeedUserAsync("a");
        var lesson = await SeedLessonAsync(userId);
        using var factory = fixture.CreateApiFactory();
        using var client = Authed(factory, tokens[0]);
        var id = Guid.NewGuid();

        var first = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", new { body = "written offline", id });
        // The retry after a lost reply (#124): same id, so the note that was already saved comes back.
        var retry = await client.PostAsJsonAsync($"/lessons/{lesson.Id}/notes", new { body = "written offline", id });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(id, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var notes = await client.GetFromJsonAsync<JsonElement[]>($"/lessons/{lesson.Id}/notes");
        Assert.Equal(id, Assert.Single(notes!).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_client_id_already_used_by_another_user_is_a_409()
    {
        var (ownerId, ownerTokens) = await SeedUserAsync("owner");
        var (intruderId, intruderTokens) = await SeedUserAsync("intruder");
        var ownerLesson = await SeedLessonAsync(ownerId);
        var intruderLesson = await SeedLessonAsync(intruderId);
        using var factory = fixture.CreateApiFactory();
        using var owner = Authed(factory, ownerTokens[0]);
        using var intruder = Authed(factory, intruderTokens[0]);
        var id = Guid.NewGuid();
        await owner.PostAsJsonAsync($"/lessons/{ownerLesson.Id}/notes", new { body = "private", id });

        var response = await intruder.PostAsJsonAsync($"/lessons/{intruderLesson.Id}/notes", new { body = "x", id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain("private", await response.Content.ReadAsStringAsync());
    }
}
