using TimeForYou.Tests.Infrastructure;

namespace TimeForYou.Tests;

/// <summary>Proves the fixture's mock container (built from mock/Dockerfile) is reachable (issue #27 AC).</summary>
[Collection(SeamACollection.Name)]
public sealed class MockSmokeTests(SeamAFixture fixture)
{
    [Fact]
    public async Task Qr_payload_endpoint_returns_success()
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.MockBaseUrl) };

        var response = await client.GetAsync("/qr-payload");

        Assert.True(response.IsSuccessStatusCode, $"expected 2xx, got {(int)response.StatusCode}");
    }
}
