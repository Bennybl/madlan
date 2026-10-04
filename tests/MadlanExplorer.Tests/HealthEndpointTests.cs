using Microsoft.AspNetCore.Mvc.Testing;
using MadlanExplorer;
using Xunit;

namespace MadlanExplorer.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
    }
}
