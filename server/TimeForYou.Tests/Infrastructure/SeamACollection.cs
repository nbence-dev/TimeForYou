namespace TimeForYou.Tests.Infrastructure;

/// <summary>
/// Every Seam A test class shares one <see cref="SeamAFixture"/> (containers start once per run,
/// not per class). xUnit never runs test classes within the same collection in parallel with each
/// other, which is exactly what's needed here: #28's mock reset endpoint hasn't landed, so until
/// it does, tests that mutate mock state must not run interleaved with each other, and the shared
/// Postgres tables are reset between tests within a class regardless.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SeamACollection : ICollectionFixture<SeamAFixture>
{
    public const string Name = "Seam A";
}
