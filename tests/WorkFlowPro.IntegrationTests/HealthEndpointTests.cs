using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WorkFlowPro.Application.Common.Models;
using Xunit;

namespace WorkFlowPro.IntegrationTests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task BuiltInHealthEndpoints_ReturnSuccess(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task ApiHealthEndpoint_ReturnsExpectedJson()
    {
        var response = await _client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        Assert.NotNull(result);
        Assert.Equal("Healthy", result.Status);
        Assert.Equal("1.0.0", result.Version);
        Assert.True(result.TimestampUtc <= DateTime.UtcNow);
    }
}
