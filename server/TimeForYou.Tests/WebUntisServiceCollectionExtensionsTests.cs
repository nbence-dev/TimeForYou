using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeForYou.WebUntis;

namespace TimeForYou.Tests;

/// <summary>Pure DI coverage for AddWebUntisClient (issue #30) — no containers needed.</summary>
public sealed class WebUntisServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_IWebUntisClient_resolvable_as_WebUntisClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WEBUNTIS_BASE_URL"] = "http://mock:8000" })
            .Build();
        var services = new ServiceCollection().AddWebUntisClient(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IWebUntisClient>();

        Assert.IsType<WebUntisClient>(client);
    }

    [Fact]
    public void Throws_when_WEBUNTIS_BASE_URL_is_not_configured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddWebUntisClient(configuration));
    }
}
