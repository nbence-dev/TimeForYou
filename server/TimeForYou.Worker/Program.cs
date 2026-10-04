namespace TimeForYou.Worker;

// An explicit entry point rather than top-level statements: the generated global `Program` would
// collide with TimeForYou.Api's in the test project, which references both.
internal static class WorkerEntryPoint
{
    private static void Main(string[] args) => WorkerApp.Build(args).Run();
}
