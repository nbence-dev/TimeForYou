using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>Proves TimeForYou.Api boots in-process via WebApplicationFactory (issue #27 AC).</summary>
[Collection(SeamACollection.Name)]
public sealed class ApiSmokeTests(SeamAFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var factory = fixture.CreateApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode, $"expected 2xx, got {(int)response.StatusCode}");
    }
}
