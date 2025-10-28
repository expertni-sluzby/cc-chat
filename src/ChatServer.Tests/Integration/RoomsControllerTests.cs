using System.Net;
using System.Net.Http.Json;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChatServer.Tests.Integration;

/// <summary>
/// Integration tests for RoomsController
/// </summary>
public class RoomsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RoomsControllerTests(WebApplicationFactory<Program> factory)
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
    public async Task CreateRoom_ValidRequest_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var request = new
        {
            name = "TestRoom",
            description = "Test Description",
            createdBy = creator
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rooms", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        room.Should().NotBeNull();
        room!.Name.Should().Be("TestRoom");
        room.Description.Should().Be("Test Description");
        room.CreatedBy.Should().Be(creator);
        room.ParticipantCount.Should().Be(0);
        room.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateRoom_UserNotFound_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            name = "TestRoom",
            description = "Test Description",
            createdBy = "nonexistent"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rooms", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("")] // empty
    public async Task CreateRoom_InvalidName_Returns400BadRequest(string name)
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var request = new
        {
            name,
            description = "Test",
            createdBy = creator
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rooms", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllRooms_ReturnsAllRooms()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        // Create two rooms
        await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"Room1_{Guid.NewGuid():N}".Substring(0, 20),
            description = "Desc1",
            createdBy = creator
        });
        await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"Room2_{Guid.NewGuid():N}".Substring(0, 20),
            description = "Desc2",
            createdBy = creator
        });

        // Act
        var response = await client.GetAsync("/api/rooms");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        rooms.Should().NotBeNull();
        rooms!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetRoomById_ExistingRoom_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act
        var response = await client.GetAsync($"/api/rooms/{createdRoom!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var room = await response.Content.ReadFromJsonAsync<RoomDetailResponse>();
        room.Should().NotBeNull();
        room!.Id.Should().Be(createdRoom.Id);
        room.Name.Should().Be("TestRoom");
        room.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoomById_NonExistentRoom_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRoom_ByCreator_Returns204NoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act
        var response = await client.DeleteAsync($"/api/rooms/{createdRoom!.Id}?requestingUser={creator}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify room is deleted
        var getResponse = await client.GetAsync($"/api/rooms/{createdRoom.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRoom_NotCreator_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var otherUser = await RegisterUniqueUser(client, "other");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act
        var response = await client.DeleteAsync($"/api/rooms/{createdRoom!.Id}?requestingUser={otherUser}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRoom_NonExistentRoom_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);

        // Act
        var response = await client.DeleteAsync($"/api/rooms/{Guid.NewGuid()}?requestingUser={user}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinRoom_ValidUser_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user = await RegisterUniqueUser(client, "user");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{createdRoom!.Id}/join", new { nickname = user });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is in participants
        var roomResponse = await client.GetAsync($"/api/rooms/{createdRoom.Id}");
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomDetailResponse>();
        room!.Participants.Should().Contain(user);
    }

    [Fact]
    public async Task JoinRoom_NonExistentUser_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{createdRoom!.Id}/join", new { nickname = "nonexistent" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinRoom_NonExistentRoom_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user = await RegisterUniqueUser(client);

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/join", new { nickname = user });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LeaveRoom_ValidUser_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user = await RegisterUniqueUser(client, "user");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Join first
        await client.PostAsJsonAsync($"/api/rooms/{createdRoom!.Id}/join", new { nickname = user });

        // Act
        var response = await client.PostAsJsonAsync($"/api/rooms/{createdRoom.Id}/leave", new { nickname = user });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is not in participants
        var roomResponse = await client.GetAsync($"/api/rooms/{createdRoom.Id}");
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomDetailResponse>();
        room!.Participants.Should().NotContain(user);
    }

    [Fact]
    public async Task LeaveRoom_UserNotInRoom_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user = await RegisterUniqueUser(client, "user");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Act - Leave without joining
        var response = await client.PostAsJsonAsync($"/api/rooms/{createdRoom!.Id}/leave", new { nickname = user });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinRoom_UserInAnotherRoom_AutomaticallyLeavesOldRoom()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user = await RegisterUniqueUser(client, "user");

        // Create two rooms
        var room1Response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "Room1",
            description = "Test1",
            createdBy = creator
        });
        var room1 = await room1Response.Content.ReadFromJsonAsync<RoomResponse>();

        var room2Response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "Room2",
            description = "Test2",
            createdBy = creator
        });
        var room2 = await room2Response.Content.ReadFromJsonAsync<RoomResponse>();

        // Join room1
        await client.PostAsJsonAsync($"/api/rooms/{room1!.Id}/join", new { nickname = user });

        // Act - Join room2
        var response = await client.PostAsJsonAsync($"/api/rooms/{room2!.Id}/join", new { nickname = user });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is in room2
        var room2DetailResponse = await client.GetAsync($"/api/rooms/{room2.Id}");
        var room2Detail = await room2DetailResponse.Content.ReadFromJsonAsync<RoomDetailResponse>();
        room2Detail!.Participants.Should().Contain(user);

        // Verify user is NOT in room1
        var room1DetailResponse = await client.GetAsync($"/api/rooms/{room1.Id}");
        var room1Detail = await room1DetailResponse.Content.ReadFromJsonAsync<RoomDetailResponse>();
        room1Detail!.Participants.Should().NotContain(user);
    }

    [Fact]
    public async Task CreateRoom_ValidatesDescription_MaxLength200()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var longDescription = new string('x', 201); // 201 characters

        var request = new
        {
            name = "TestRoom",
            description = longDescription,
            createdBy = creator
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rooms", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRoom_WithParticipants_DeletesSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();
        var creator = await RegisterUniqueUser(client, "creator");
        var user1 = await RegisterUniqueUser(client, "user1");
        var user2 = await RegisterUniqueUser(client, "user2");

        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "TestRoom",
            description = "Test",
            createdBy = creator
        });
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Add participants
        await client.PostAsJsonAsync($"/api/rooms/{createdRoom!.Id}/join", new { nickname = user1 });
        await client.PostAsJsonAsync($"/api/rooms/{createdRoom.Id}/join", new { nickname = user2 });

        // Act
        var response = await client.DeleteAsync($"/api/rooms/{createdRoom.Id}?requestingUser={creator}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify room is deleted
        var getResponse = await client.GetAsync($"/api/rooms/{createdRoom.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
