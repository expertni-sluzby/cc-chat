using System.Net;
using System.Net.Http.Json;
using ChatServer.Models;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChatServer.Tests.Integration;

/// <summary>
/// Integration tests for MessagesController
/// </summary>
public class MessagesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MessagesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<string> RegisterUniqueUser(HttpClient client, string? prefix = null)
    {
        var nickname = $"{prefix ?? "user"}_{Guid.NewGuid():N}".Substring(0, 20);
        await client.PostAsJsonAsync("/api/users/register", new { nickname });
        return nickname;
    }

    private async Task<RoomResponse> CreateRoom(HttpClient client, string creator)
    {
        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"Room_{Guid.NewGuid():N}".Substring(0, 20),
            description = "Test",
            createdBy = creator
        });

        return (await response.Content.ReadFromJsonAsync<RoomResponse>())!;
    }

    private async Task<RoomResponse> CreateRoomAndJoinUser(HttpClient client, string user)
    {
        var room = await CreateRoom(client, user);
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/join", new { nickname = user });
        return room;
    }

    [Fact]
    public async Task SendMessage_ValidRequest_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);
        var request = new { author = user, content = "Hello!" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>();
        message.Should().NotBeNull();
        message!.Content.Should().Be("Hello!");
        message.Author.Should().Be(user);
        message.Type.Should().Be(MessageType.UserMessage);
        message.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task SendMessage_RoomNotFound_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var request = new { author = user, content = "Hello!" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendMessage_UserNotInRoom_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var outsider = await RegisterUniqueUser(client, "outsider");
        var room = await CreateRoom(client, creator);
        var request = new { author = outsider, content = "Hello!" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task SendMessage_EmptyContent_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);
        var request = new { author = user, content = "" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_ContentTooLong_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);
        var longContent = new string('x', 1001);
        var request = new { author = user, content = longContent };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMessages_EmptyRoom_ReturnsEmptyList()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoom(client, user);

        // Act
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
        messages.Should().NotBeNull();
        messages!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMessages_RoomNotFound_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}/messages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMessages_AfterSendingMessages_ReturnsMessagesInOrder()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);

        // Send messages
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = "First" });
        await Task.Delay(10);
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = "Second" });
        await Task.Delay(10);
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = "Third" });

        // Act
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
        messages.Should().NotBeNull();
        messages!.Count.Should().BeGreaterThanOrEqualTo(3);

        // Find our messages (might have system message from join)
        var userMessages = messages.Where(m => m.Type == MessageType.UserMessage).ToList();
        userMessages.Should().HaveCount(3);
        userMessages[0].Content.Should().Be("First");
        userMessages[1].Content.Should().Be("Second");
        userMessages[2].Content.Should().Be("Third");
    }

    [Fact]
    public async Task GetMessages_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);

        // Send 5 messages
        for (int i = 1; i <= 5; i++)
        {
            await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = $"Message {i}" });
        }

        // Act
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages?limit=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
        messages.Should().NotBeNull();

        // Should return last 3 user messages (plus potentially system message)
        var userMessages = messages!.Where(m => m.Type == MessageType.UserMessage).ToList();
        userMessages.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Integration_JoinRoom_CreatesSystemMessage()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user = await RegisterUniqueUser(client, "user");
        var room = await CreateRoom(client, creator);

        // Act
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/join", new { nickname = user });

        // Get messages
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();

        // Assert
        messages.Should().NotBeNull();
        messages!.Should().HaveCount(1);
        var msg = messages[0];
        msg.Type.Should().Be(MessageType.UserJoined);
        msg.Author.Should().Be("SYSTEM");
        msg.Content.Should().Contain(user);
        msg.Content.Should().Contain("vstoupil");
    }

    [Fact]
    public async Task Integration_LeaveRoom_CreatesSystemMessage()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);

        // Act
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/leave", new { nickname = user });

        // Get messages
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();

        // Assert
        messages.Should().NotBeNull();
        messages!.Should().HaveCount(2); // Join + Leave
        messages[0].Type.Should().Be(MessageType.UserJoined);
        messages[1].Type.Should().Be(MessageType.UserLeft);
        messages[1].Author.Should().Be("SYSTEM");
        messages[1].Content.Should().Contain(user);
        messages[1].Content.Should().Contain("opustil");
    }

    [Fact]
    public async Task Integration_FullFlow_JoinSendLeave()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);

        // Act
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = "Hello!" });
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user, content = "Goodbye!" });
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/leave", new { nickname = user });

        // Get messages
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();

        // Assert
        messages.Should().NotBeNull();
        messages!.Should().HaveCount(4); // Join + 2 user messages + Leave
        messages[0].Type.Should().Be(MessageType.UserJoined);
        messages[1].Type.Should().Be(MessageType.UserMessage);
        messages[1].Content.Should().Be("Hello!");
        messages[2].Type.Should().Be(MessageType.UserMessage);
        messages[2].Content.Should().Be("Goodbye!");
        messages[3].Type.Should().Be(MessageType.UserLeft);
    }

    [Fact]
    public async Task SendMessage_ContentWithHtml_EncodesHtml()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);
        var room = await CreateRoomAndJoinUser(client, user);
        var request = new { author = user, content = "<script>alert('xss')</script>" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>();
        message.Should().NotBeNull();
        message!.Content.Should().Contain("&lt;").And.Contain("&gt;");
        message.Content.Should().NotContain("<script>");
    }

    [Fact]
    public async Task SendMessage_MultipleUsersInRoom_AllCanSend()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user1 = await RegisterUniqueUser(client, "user1");
        var user2 = await RegisterUniqueUser(client, "user2");
        var room = await CreateRoom(client, user1);

        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/join", new { nickname = user1 });
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/join", new { nickname = user2 });

        // Act
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user1, content = "Hello from user1" });
        await client.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new { author = user2, content = "Hello from user2" });

        // Get messages
        var response = await client.GetAsync($"/api/rooms/{room.Id}/messages");
        var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();

        // Assert
        var userMessages = messages!.Where(m => m.Type == MessageType.UserMessage).ToList();
        userMessages.Should().HaveCount(2);
        userMessages[0].Author.Should().Be(user1);
        userMessages[1].Author.Should().Be(user2);
    }
}
