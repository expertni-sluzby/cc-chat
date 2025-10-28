using System.Net.Http.Json;
using ChatServer.Models;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace ChatServer.Tests.E2E;

/// <summary>
/// End-to-end tests for complete chat sessions
/// </summary>
public class CompleteChatSessionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CompleteChatSessionTests(WebApplicationFactory<Program> factory)
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
    public async Task E2E_CompleteChatSession_TwoUsersExchangeMessages()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;

        // Get the base address
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        // Register two users
        var alice = await RegisterUniqueUser(httpClient, "alice");
        var bob = await RegisterUniqueUser(httpClient, "bob");

        // Create room
        var roomResponse = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "E2E Test Room",
            description = "Complete chat session test",
            createdBy = alice
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();
        room.Should().NotBeNull();

        // Setup WebSocket connections
        var aliceConnection = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var bobConnection = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var aliceMessages = new List<MessageResponse>();
        var bobMessages = new List<MessageResponse>();
        var aliceJoinEvents = new List<(string nickname, string roomId)>();
        var bobJoinEvents = new List<(string nickname, string roomId)>();
        var aliceLeaveEvents = new List<(string nickname, string roomId)>();

        aliceConnection.On<MessageResponse>("ReceiveMessage", msg => aliceMessages.Add(msg));
        bobConnection.On<MessageResponse>("ReceiveMessage", msg => bobMessages.Add(msg));
        aliceConnection.On<string, string>("UserJoined", (nickname, roomId) => aliceJoinEvents.Add((nickname, roomId)));
        bobConnection.On<string, string>("UserJoined", (nickname, roomId) => bobJoinEvents.Add((nickname, roomId)));
        aliceConnection.On<string, string>("UserLeft", (nickname, roomId) => aliceLeaveEvents.Add((nickname, roomId)));

        // Act
        // 1. Both users connect
        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();

        // 2. Alice joins room
        await aliceConnection.InvokeAsync("JoinRoom", room!.Id.ToString(), alice);
        await Task.Delay(300); // Wait for system message

        // 3. Bob joins room
        await bobConnection.InvokeAsync("JoinRoom", room.Id.ToString(), bob);
        await Task.Delay(300); // Wait for join notification

        // 4. Exchange messages
        await aliceConnection.InvokeAsync("SendMessage", room.Id.ToString(), "Hello Bob!");
        await Task.Delay(200);

        await bobConnection.InvokeAsync("SendMessage", room.Id.ToString(), "Hi Alice!");
        await Task.Delay(200);

        await aliceConnection.InvokeAsync("SendMessage", room.Id.ToString(), "How are you?");
        await Task.Delay(200);

        // 5. Alice leaves
        await aliceConnection.InvokeAsync("LeaveRoom", room.Id.ToString(), alice);
        await Task.Delay(200);

        // Assert
        // Alice should NOT receive her own join system message (by design - she gets RoomHistory instead)
        // Alice should have received Bob's join via UserJoined event (not via ReceiveMessage)
        // System messages are NOT broadcast via ReceiveMessage, only as dedicated events
        aliceJoinEvents.Should().Contain(e => e.nickname == bob);

        // Both should have received all user messages
        aliceMessages.Should().Contain(m => m.Content == "Hello Bob!" && m.Author == alice);
        aliceMessages.Should().Contain(m => m.Content == "Hi Alice!" && m.Author == bob);
        aliceMessages.Should().Contain(m => m.Content == "How are you?" && m.Author == alice);

        bobMessages.Should().Contain(m => m.Content == "Hello Bob!" && m.Author == alice);
        bobMessages.Should().Contain(m => m.Content == "Hi Alice!" && m.Author == bob);
        bobMessages.Should().Contain(m => m.Content == "How are you?" && m.Author == alice);

        // Bob should NOT have received Alice's leave event (OthersInGroup)
        // But Alice's connection should have triggered the leave successfully

        // Cleanup
        await aliceConnection.StopAsync();
        await bobConnection.StopAsync();
        await aliceConnection.DisposeAsync();
        await bobConnection.DisposeAsync();
    }

    [Fact]
    public async Task E2E_MultiRoom_UserSwitchesBetweenRooms()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        var user = await RegisterUniqueUser(httpClient);

        // Create two rooms
        var room1Response = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Room 1",
            description = "First room",
            createdBy = user
        });
        var room1 = await room1Response.Content.ReadFromJsonAsync<RoomResponse>();

        var room2Response = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Room 2",
            description = "Second room",
            createdBy = user
        });
        var room2 = await room2Response.Content.ReadFromJsonAsync<RoomResponse>();

        // Setup connection
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var messages = new List<MessageResponse>();
        connection.On<MessageResponse>("ReceiveMessage", msg => messages.Add(msg));

        await connection.StartAsync();

        // Act
        // 1. Join room 1
        await connection.InvokeAsync("JoinRoom", room1!.Id.ToString(), user);
        await Task.Delay(100);

        // 2. Send message in room 1
        await connection.InvokeAsync("SendMessage", room1.Id.ToString(), "Message in room 1");
        await Task.Delay(100);

        // 3. Join room 2 (should auto-leave room 1)
        await connection.InvokeAsync("JoinRoom", room2!.Id.ToString(), user);
        await Task.Delay(100);

        // 4. Send message in room 2
        await connection.InvokeAsync("SendMessage", room2.Id.ToString(), "Message in room 2");
        await Task.Delay(100);

        // Assert
        messages.Should().Contain(m => m.Content == "Message in room 1" && m.RoomId == room1.Id);
        messages.Should().Contain(m => m.Content == "Message in room 2" && m.RoomId == room2.Id);

        // Verify user left room 1 via REST API
        var room1Detail = await httpClient.GetFromJsonAsync<RoomDetailResponse>($"/api/rooms/{room1.Id}");
        room1Detail!.Participants.Should().NotContain(user);

        // Verify user is in room 2
        var room2Detail = await httpClient.GetFromJsonAsync<RoomDetailResponse>($"/api/rooms/{room2.Id}");
        room2Detail!.Participants.Should().Contain(user);

        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task E2E_RESTAndWebSocket_BothWorkTogether()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        var restUser = await RegisterUniqueUser(httpClient, "rest");
        var wsUser = await RegisterUniqueUser(httpClient, "ws");

        // Create room
        var roomResponse = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Hybrid Test Room",
            description = "REST + WebSocket test",
            createdBy = restUser
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // REST user joins via REST
        await httpClient.PostAsJsonAsync($"/api/rooms/{room!.Id}/join", new { nickname = restUser });

        // WebSocket user connects
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var wsMessages = new List<MessageResponse>();
        connection.On<MessageResponse>("ReceiveMessage", msg => wsMessages.Add(msg));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinRoom", room.Id.ToString(), wsUser);
        await Task.Delay(100);

        // Act
        // 1. REST user sends message via REST
        await httpClient.PostAsJsonAsync($"/api/rooms/{room.Id}/messages", new
        {
            author = restUser,
            content = "Message via REST"
        });
        await Task.Delay(200); // Wait for broadcast

        // 2. WebSocket user sends via WebSocket
        await connection.InvokeAsync("SendMessage", room.Id.ToString(), "Message via WebSocket");
        await Task.Delay(100);

        // 3. Get messages via REST
        var restMessages = await httpClient.GetFromJsonAsync<List<MessageResponse>>($"/api/rooms/{room.Id}/messages");

        // Assert
        // WebSocket user should have received both messages
        wsMessages.Should().Contain(m => m.Content == "Message via REST" && m.Author == restUser);
        wsMessages.Should().Contain(m => m.Content == "Message via WebSocket" && m.Author == wsUser);

        // REST API should return all messages
        restMessages.Should().Contain(m => m.Content == "Message via REST");
        restMessages.Should().Contain(m => m.Content == "Message via WebSocket");

        // Cleanup
        await connection.StopAsync();
        await connection.DisposeAsync();
    }
}
