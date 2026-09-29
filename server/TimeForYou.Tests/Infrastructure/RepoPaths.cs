namespace TimeForYou.Tests.Infrastructure;

/// <summary>
/// Locates repo-relative paths from the test run's bin directory, which moves around depending
/// on configuration and CI checkout location. Walks up from <see cref="AppContext.BaseDirectory"/>
/// looking for <c>TimeForYou.slnx</c>, the one fixed landmark for "this is server/".
/// </summary>
internal static class RepoPaths
{
    public static string MockContextDirectory => Path.GetFullPath(Path.Combine(ServerDirectory, "..", "mock"));

    private static string ServerDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TimeForYou.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName
                ?? throw new InvalidOperationException(
                    $"Could not locate server/TimeForYou.slnx above {AppContext.BaseDirectory}.");
        }
    }
}
