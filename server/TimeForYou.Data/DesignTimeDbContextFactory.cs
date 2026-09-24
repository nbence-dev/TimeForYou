using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeForYou.Data;

/// <summary>
/// Lets `dotnet ef migrations add` run against this project directly, without needing api/worker's
/// own DI wiring (out of scope for #14). Never used at runtime — api/worker configure
/// TimeForYouDbContext from ConnectionStrings__TimeForYou themselves.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimeForYouDbContext>
{
    public TimeForYouDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__TimeForYou")
            ?? "Host=localhost;Port=5432;Database=timeforyou;Username=timeforyou;Password=design-time-only";

        var optionsBuilder = new DbContextOptionsBuilder<TimeForYouDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TimeForYouDbContext(optionsBuilder.Options);
    }
}
