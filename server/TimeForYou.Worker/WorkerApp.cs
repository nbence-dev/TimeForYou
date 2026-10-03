using Hangfire;
using TimeForYou.Worker.Polling;

namespace TimeForYou.Worker;

/// <summary>
/// The worker as a web host: Hangfire's server and the recurring poll keep running as hosted
/// services, and the host adds the LAN-only operator endpoints — <c>POST /nudge</c>, the Hangfire
/// dashboard at <c>/hangfire</c>, and <c>/healthz</c> for compose. None of it is routed through
/// cloudflared; only the API is public.
/// </summary>
public static class WorkerApp
{
    public const string HealthPath = "/healthz";

    /// <param name="configure">Test hook: runs after the module is registered, before the host is built.</param>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddWorkerModule(builder.Configuration);
        builder.Services.AddHealthChecks();
        configure?.Invoke(builder);

        var app = builder.Build();

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new LanDashboardAuthorizationFilter()],
        });
        app.MapHealthChecks(HealthPath);
        app.MapPost("/nudge", (HttpContext context, IBackgroundJobClient jobs) =>
        {
            // Compose publishes this port on every host interface, so enforce LAN-only here too.
            if (!LanDashboardAuthorizationFilter.IsLan(context.Connection.RemoteIpAddress))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var jobId = jobs.Enqueue<PollJob>(job => job.RunAsync(CancellationToken.None));
            return Results.Accepted(value: new { job_id = jobId });
        });

        return app;
    }
}
