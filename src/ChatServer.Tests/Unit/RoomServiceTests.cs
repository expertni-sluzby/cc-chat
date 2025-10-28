using ChatServer.Models;
using ChatServer.Services;
using ChatServer.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Unit tests for RoomService
/// </summary>
public class RoomServiceTests
{
    private RoomService CreateRoomService(IDataStore? dataStore = null)
    {
        var store = dataStore ?? new InMemoryDataStore();
        var logger = new Mock<ILogger<RoomService>>().Object;
        return new RoomService(store, logger);
    }

    private UserService CreateUserService(IDataStore dataStore)
    {
        return new UserService(dataStore);
    }

    [Fact]
    public async Task CreateRoom_ValidData_ReturnsRoom()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");

        // Act
        var room = await roomService.CreateRoomAsync("TestRoom", "Description", "creator");

        // Assert
        room.Should().NotBeNull();
        room!.Name.Should().Be("TestRoom");
        room.Description.Should().Be("Description");
        room.CreatedBy.Should().Be("creator");
        room.Participants.Should().BeEmpty();
        room.Id.Should().NotBe(Guid.Empty);
        room.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("a")] // too short
    [InlineData("")] // empty
    [InlineData("123456789012345678901234567890123456789012345678901")] // too long (51 chars)
    public async Task CreateRoom_InvalidNameLength_ReturnsNull(string name)
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");

        // Act
        var room = await roomService.CreateRoomAsync(name, "Description", "creator");

        // Assert
        room.Should().BeNull();
    }

    [Fact]
    public async Task CreateRoom_DescriptionTooLong_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        var longDescription = new string('x', 201); // 201 characters

        // Act
        var room = await roomService.CreateRoomAsync("TestRoom", longDescription, "creator");

        // Assert
        room.Should().BeNull();
    }

    [Fact]
    public async Task CreateRoom_UserNotFound_ReturnsNull()
    {
        // Arrange
        var roomService = CreateRoomService();

        // Act
        var room = await roomService.CreateRoomAsync("TestRoom", "Description", "nonexistent");

        // Assert
        room.Should().BeNull();
    }

    [Fact]
    public async Task GetAllRooms_ReturnsAllRooms()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await roomService.CreateRoomAsync("Room1", "Desc1", "creator");
        await roomService.CreateRoomAsync("Room2", "Desc2", "creator");

        // Act
        var rooms = await roomService.GetAllRoomsAsync();

        // Assert
        rooms.Should().HaveCount(2);
        rooms.Select(r => r.Name).Should().Contain(new[] { "Room1", "Room2" });
    }

    [Fact]
    public async Task GetRoomById_ExistingRoom_ReturnsRoom()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        var created = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var room = await roomService.GetRoomByIdAsync(created!.Id);

        // Assert
        room.Should().NotBeNull();
        room!.Id.Should().Be(created.Id);
        room.Name.Should().Be("TestRoom");
    }

    [Fact]
    public async Task GetRoomById_NonExistentRoom_ReturnsNull()
    {
        // Arrange
        var roomService = CreateRoomService();

        // Act
        var room = await roomService.GetRoomByIdAsync(Guid.NewGuid());

        // Assert
        room.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoom_ByCreator_ReturnsTrue()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.DeleteRoomAsync(room!.Id, "creator");

        // Assert
        result.Should().BeTrue();
        var deletedRoom = await roomService.GetRoomByIdAsync(room.Id);
        deletedRoom.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoom_NotCreator_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("otheruser");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.DeleteRoomAsync(room!.Id, "otheruser");

        // Assert
        result.Should().BeFalse();
        var stillExists = await roomService.GetRoomByIdAsync(room.Id);
        stillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteRoom_NonExistentRoom_ReturnsFalse()
    {
        // Arrange
        var roomService = CreateRoomService();

        // Act
        var result = await roomService.DeleteRoomAsync(Guid.NewGuid(), "someone");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task JoinRoom_ValidUser_AddsToParticipants()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.JoinRoomAsync(room!.Id, "user1");

        // Assert
        result.Should().BeTrue();
        var updatedRoom = await roomService.GetRoomByIdAsync(room.Id);
        updatedRoom!.Participants.Should().Contain("user1");

        // Check user's current room is set
        var currentRoom = await roomService.GetUserCurrentRoomAsync("user1");
        currentRoom.Should().Be(room.Id);
    }

    [Fact]
    public async Task JoinRoom_UserAlreadyInRoom_ReturnsTrue()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        var result = await roomService.JoinRoomAsync(room.Id, "user1");

        // Assert
        result.Should().BeTrue();
        var updatedRoom = await roomService.GetRoomByIdAsync(room.Id);
        updatedRoom!.Participants.Should().Contain("user1");
        updatedRoom.Participants.Count(p => p == "user1").Should().Be(1); // Only once
    }

    [Fact]
    public async Task JoinRoom_UserInAnotherRoom_AutomaticallyLeavesOldRoom()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room1 = await roomService.CreateRoomAsync("Room1", "Desc1", "creator");
        var room2 = await roomService.CreateRoomAsync("Room2", "Desc2", "creator");
        await roomService.JoinRoomAsync(room1!.Id, "user1");

        // Act
        var result = await roomService.JoinRoomAsync(room2!.Id, "user1");

        // Assert
        result.Should().BeTrue();

        var oldRoom = await roomService.GetRoomByIdAsync(room1.Id);
        var newRoom = await roomService.GetRoomByIdAsync(room2.Id);

        oldRoom!.Participants.Should().NotContain("user1");
        newRoom!.Participants.Should().Contain("user1");

        // Check user's current room is updated
        var currentRoom = await roomService.GetUserCurrentRoomAsync("user1");
        currentRoom.Should().Be(room2.Id);
    }

    [Fact]
    public async Task JoinRoom_NonExistentRoom_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("user1");

        // Act
        var result = await roomService.JoinRoomAsync(Guid.NewGuid(), "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task JoinRoom_NonExistentUser_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.JoinRoomAsync(room!.Id, "nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveRoom_ValidUser_RemovesFromParticipants()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        var result = await roomService.LeaveRoomAsync(room.Id, "user1");

        // Assert
        result.Should().BeTrue();
        var updatedRoom = await roomService.GetRoomByIdAsync(room.Id);
        updatedRoom!.Participants.Should().NotContain("user1");

        // Check user's current room is cleared
        var currentRoom = await roomService.GetUserCurrentRoomAsync("user1");
        currentRoom.Should().BeNull();
    }

    [Fact]
    public async Task LeaveRoom_UserNotInRoom_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.LeaveRoomAsync(room!.Id, "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LeaveRoom_NonExistentRoom_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("user1");

        // Act
        var result = await roomService.LeaveRoomAsync(Guid.NewGuid(), "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserInRoom_UserInRoom_ReturnsTrue()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        var result = await roomService.IsUserInRoomAsync(room.Id, "user1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserInRoom_UserNotInRoom_ReturnsFalse()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");

        // Act
        var result = await roomService.IsUserInRoomAsync(room!.Id, "user1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserCurrentRoom_UserInRoom_ReturnsRoomId()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        var currentRoom = await roomService.GetUserCurrentRoomAsync("user1");

        // Assert
        currentRoom.Should().Be(room.Id);
    }

    [Fact]
    public async Task GetUserCurrentRoom_UserNotInRoom_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("user1");

        // Act
        var currentRoom = await roomService.GetUserCurrentRoomAsync("user1");

        // Assert
        currentRoom.Should().BeNull();
    }

    [Fact]
    public async Task GetUserCurrentRoom_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var roomService = CreateRoomService();

        // Act
        var currentRoom = await roomService.GetUserCurrentRoomAsync("nonexistent");

        // Assert
        currentRoom.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoom_WithParticipants_ClearsParticipantsCurrentRoom()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        await userService.RegisterUserAsync("user2");
        var room = await roomService.CreateRoomAsync("TestRoom", "Desc", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");
        await roomService.JoinRoomAsync(room.Id, "user2");

        // Act
        var result = await roomService.DeleteRoomAsync(room.Id, "creator");

        // Assert
        result.Should().BeTrue();

        // Check that both users' current room is cleared
        var user1CurrentRoom = await roomService.GetUserCurrentRoomAsync("user1");
        var user2CurrentRoom = await roomService.GetUserCurrentRoomAsync("user2");

        user1CurrentRoom.Should().BeNull();
        user2CurrentRoom.Should().BeNull();
    }
}
