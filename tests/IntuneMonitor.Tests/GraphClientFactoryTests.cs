using IntuneMonitor.Graph;
using Microsoft.Extensions.DependencyInjection;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for GraphClientFactory — validates HttpClient creation using IHttpClientFactory.
/// </summary>
public class GraphClientFactoryTests
{
    private static IHttpClientFactory CreateHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(GraphClientFactory.HttpClientName);
        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GraphClientFactory(null!));
    }

    [Fact]
    public void CreateHttpClient_ReturnsClientWithBearerToken()
    {
        var factory = new GraphClientFactory(CreateHttpClientFactory());
        using var client = factory.CreateHttpClient("test-token-123");

        Assert.NotNull(client);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("test-token-123", client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void CreateHttpClient_DifferentTokens_ReturnDifferentClients()
    {
        var factory = new GraphClientFactory(CreateHttpClientFactory());
        using var client1 = factory.CreateHttpClient("token-a");
        using var client2 = factory.CreateHttpClient("token-b");

        Assert.NotSame(client1, client2);
        Assert.Equal("token-a", client1.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.Equal("token-b", client2.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void HttpClientName_IsGraph()
    {
        Assert.Equal("Graph", GraphClientFactory.HttpClientName);
    }
}
