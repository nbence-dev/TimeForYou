using Hangfire;

namespace TimeForYou.Worker.Polling;

/// <summary>
/// Registers (or updates) the recurring poll job at startup, every POLL_INTERVAL_SECONDS
/// (.env.example: 600 by default, 30 for the demo).
/// </summary>
public sealed class PollJobScheduler(IRecurringJobManager recurringJobs, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var seconds = configuration.GetValue("POLL_INTERVAL_SECONDS", 600);
        recurringJobs.AddOrUpdate<PollJob>(
            PollJob.RecurringJobId,
            job => job.RunAsync(CancellationToken.None),
            CronFor(seconds),
            new RecurringJobOptions());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Under a minute uses Hangfire's six-field (seconds) cron; otherwise whole minutes.</summary>
    internal static string CronFor(int seconds) => seconds switch
    {
        > 0 and < 60 => $"*/{seconds} * * * * *",
        _ when seconds % 60 == 0 && seconds / 60 is > 0 and < 60 => $"*/{seconds / 60} * * * *",
        _ => throw new InvalidOperationException(
            $"POLL_INTERVAL_SECONDS must be 1–59 or a whole number of minutes up to 59, got {seconds}."),
    };
}