using Microsoft.EntityFrameworkCore;
using TimeForYou.Data.Entities;

namespace TimeForYou.Data;

public class TimeForYouDbContext(DbContextOptions<TimeForYouDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<LessonSnapshot> LessonSnapshots => Set<LessonSnapshot>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<IcsToken> IcsTokens => Set<IcsToken>();

    public DbSet<ChangeLogEntry> ChangeLog => Set<ChangeLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tables live in the default `public` schema. Hangfire's PostgreSQL storage adapter
        // manages its own `hangfire` schema and is never touched by this DbContext or its
        // migrations (#14 acceptance criteria).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeForYouDbContext).Assembly);
    }
}
