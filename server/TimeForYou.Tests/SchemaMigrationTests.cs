using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TimeForYou.Data;
using TimeForYou.Data.Entities;
using TimeForYou.Domain;

namespace TimeForYou.Tests;

/// <summary>
/// Throwaway Seam A coverage for #14 — the real HTTP-boundary harness (mock, sidecar, worker,
/// stubbed IPushSender) lands in Wave 2 and replaces this. Only checks that the migration in
/// TimeForYou.Data applies to a real Postgres 16 and that the lesson_snapshots upsert path
/// behaves the way the poll cycle needs.
/// </summary>
public sealed class SchemaMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private TimeForYouDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TimeForYouDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new TimeForYouDbContext(options);
    }

    [Fact]
    public async Task Migration_applies_cleanly_to_a_fresh_database()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Upserting_the_same_lesson_updates_rather_than_duplicates()
    {
        await using var setupContext = CreateContext();
        await setupContext.Database.MigrateAsync();

        var user = new User { School = "eduvos", CredentialsProtected = "ciphertext" };
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
        await using var pollContext = CreateContext();
        await pollContext.Database.ExecuteSqlInterpolatedAsync($"""
            insert into lesson_snapshots (id, user_id, untis_lesson_id, subject, room, start_ts, end_ts, status, created_at, updated_at)
            values (gen_random_uuid(), {user.Id}, 'untis-lesson-1', 'Maths', 'B99', now(), now() + interval '1 hour', 'irregular', now(), now())
            on conflict (user_id, untis_lesson_id) do update set room = excluded.room, status = excluded.status, updated_at = now()
            """);

        await using var verifyContext = CreateContext();
        var rows = await verifyContext.LessonSnapshots
            .Where(l => l.UserId == user.Id && l.UntisLessonId == "untis-lesson-1")
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal("B99", row.Room);
        Assert.Equal(LessonStatus.Irregular, row.Status);
    }
}
