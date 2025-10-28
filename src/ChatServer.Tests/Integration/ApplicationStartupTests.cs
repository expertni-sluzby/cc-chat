using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChatServer.Tests.Integration;

/// <summary>
/// Smoke tests to verify the application can start and basic infrastructure works
/// </summary>
public class ApplicationStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Application_ShouldStartSuccessfully()
    {
        // Arrange & Act
        var client = _factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldBeAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Chat Server API", content);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }
}
