using Microsoft.EntityFrameworkCore;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;
using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>
/// Migration and upsert coverage, now running on the shared Seam A fixture (issue #27) instead of
/// its own throwaway Postgres container.
/// </summary>
[Collection(SeamACollection.Name)]
public sealed class SchemaMigrationTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_applies_cleanly_to_a_fresh_database()
    {
        await using var context = fixture.CreateDbContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Upserting_the_same_lesson_updates_rather_than_duplicates()
    {
        await using var setupContext = fixture.CreateDbContext();

        var user = new User { School = "eduvos", CredentialsProtected = "ciphertext", UntisUserHash = "hash" };
        setupContext.Users.Add(user);
        setupContext.LessonSnapshots.Add(new LessonSnapshot
        {
            User = user,
            UntisLessonId = "untis-lesson-1",
            Subject = "Maths",
            Room = "B4",
            StartTs = DateTimeOffset.UtcNow,
            EndTs = DateTimeOffset.UtcNow.AddHours(1),
            Status = LessonStatus.Scheduled,
        });
        await setupContext.SaveChangesAsync();

        // Simulates the next poll cycle's upsert for the same upstream lesson, via the same
        // ON CONFLICT the worker will use — the unique (user_id, untis_lesson_id) index is what
        // makes this an update instead of a second row.
        await using var pollContext = fixture.CreateDbContext();
        await pollContext.Database.ExecuteSqlInterpolatedAsync($"""
            insert into lesson_snapshots (id, user_id, untis_lesson_id, subject, room, start_ts, end_ts, status, created_at, updated_at)
            values (gen_random_uuid(), {user.Id}, 'untis-lesson-1', 'Maths', 'B99', now(), now() + interval '1 hour', 'irregular', now(), now())
            on conflict (user_id, untis_lesson_id) do update set room = excluded.room, status = excluded.status, updated_at = now()
            """);

        await using var verifyContext = fixture.CreateDbContext();
        var rows = await verifyContext.LessonSnapshots
            .Where(l => l.UserId == user.Id && l.UntisLessonId == "untis-lesson-1")
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal("B99", row.Room);
        Assert.Equal(LessonStatus.Irregular, row.Status);
    }
}
