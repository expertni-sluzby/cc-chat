using System.Net.Http.Json;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChatServer.Tests.Integration;

/// <summary>
/// Integration tests for SignalR broadcasting from REST API
/// </summary>
public class SignalRIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SignalRIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<string> RegisterUniqueUser(HttpClient client, string? prefix = null)
    {
        var nickname = $"{prefix ?? "user"}_{Guid.NewGuid():N}".Substring(0, 20);
        await client.PostAsJsonAsync("/api/users/register", new { nickname });
        return nickname;
    }

    [Fact]
    public async Task ChatHub_IsAccessible()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - try to access the hub endpoint (should exist even if we can't connect via HTTP)
        var response = await client.GetAsync("/hubs/chat");

        // Assert - SignalR hubs don't respond to regular HTTP GET, but the endpoint should exist
        // We expect either 400 (Bad Request) or 405 (Method Not Allowed) or similar, not 404
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestAPI_RoomCreation_BroadcastingConfigured()
    {
        // This test verifies that the hub context is properly injected
        // and that broadcasting code is in place (even if we can't test the actual broadcast without a WS client)

        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        // Act - Create room (this should trigger broadcasting)
        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "BroadcastTest",
            description = "Test",
            createdBy = creator
        });

        // Assert - Should succeed, which means hub context is properly injected
        response.IsSuccessStatusCode.Should().BeTrue();
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        room.Should().NotBeNull();
    }

    [Fact]
    public async Task RestAPI_MessageSending_BroadcastingConfigured()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);

        // Create room and join
        var roomResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = user
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

        await client.PostAsJsonAsync($"/api/rooms/{room!.Id}/join", new { nickname = user });

        // Act - Send message (this should trigger broadcasting)
        var messageResponse = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new
        {
            author = user,
            content = "Test message for broadcasting"
        });

        // Assert - Should succeed, which means hub context is properly injected
        messageResponse.IsSuccessStatusCode.Should().BeTrue();
        var message = await messageResponse.Content.ReadFromJsonAsync<MessageResponse>();
        message.Should().NotBeNull();
        message!.Content.Should().Contain("Test message");
    }

    [Fact]
    public async Task RestAPI_RoomDeletion_BroadcastingConfigured()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        var roomResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "ToDelete",
            description = "Test",
            createdBy = creator
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act - Delete room (this should trigger broadcasting)
        var deleteResponse = await client.DeleteAsync($"/api/rooms/{room!.Id}?requestingUser={creator}");

        // Assert - Should succeed, which means hub context is properly injected
        deleteResponse.IsSuccessStatusCode.Should().BeTrue();
    }
}
