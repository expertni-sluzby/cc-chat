using ChatServer.Models;
using ChatServer.Storage;
using FluentAssertions;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Tests for InMemoryDataStore to verify thread-safety and basic operations
/// </summary>
public class InMemoryDataStoreTests
{
    [Fact]
    public void AddUser_ShouldStoreUser()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var user = new User
        {
            Nickname = "alice",
            RegisteredAt = DateTime.UtcNow,
            IsOnline = true
        };

        // Act
        var result = store.TryAddUser("alice", user);

        // Assert
        result.Should().BeTrue();
        store.UserCount.Should().Be(1);
        store.TryGetUser("alice", out var retrievedUser).Should().BeTrue();
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Nickname.Should().Be("alice");
    }

    [Fact]
    public void AddUser_WithSameNickname_ShouldFail()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var user1 = new User { Nickname = "alice", RegisteredAt = DateTime.UtcNow };
        var user2 = new User { Nickname = "alice", RegisteredAt = DateTime.UtcNow };

        // Act
        var result1 = store.TryAddUser("alice", user1);
        var result2 = store.TryAddUser("alice", user2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeFalse();
        store.UserCount.Should().Be(1);
    }

    [Fact]
    public void AddRoom_ShouldStoreRoom()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var roomId = Guid.NewGuid();
        var room = new ChatRoom
        {
            Id = roomId,
            Name = "General",
            CreatedBy = "alice",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = store.TryAddRoom(roomId, room);

        // Assert
        result.Should().BeTrue();
        store.RoomCount.Should().Be(1);
        store.TryGetRoom(roomId, out var retrievedRoom).Should().BeTrue();
        retrievedRoom.Should().NotBeNull();
        retrievedRoom!.Name.Should().Be("General");
    }

    [Fact]
    public void AddMessage_ShouldStoreMessage()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var roomId = Guid.NewGuid();
        var message = new Message
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Author = "alice",
            Content = "Hello!",
            Type = MessageType.UserMessage,
            Timestamp = DateTime.UtcNow
        };

        // Act
        store.AddMessage(roomId, message);

        // Assert
        var messages = store.GetRoomMessages(roomId).ToList();
        messages.Should().HaveCount(1);
        messages[0].Content.Should().Be("Hello!");
    }

    [Fact]
    public void Clear_ShouldRemoveAllData()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var user = new User { Nickname = "alice", RegisteredAt = DateTime.UtcNow };
        var roomId = Guid.NewGuid();
        var room = new ChatRoom { Id = roomId, Name = "General", CreatedBy = "alice", CreatedAt = DateTime.UtcNow };

        store.TryAddUser("alice", user);
        store.TryAddRoom(roomId, room);

        // Act
        store.Clear();

        // Assert
        store.UserCount.Should().Be(0);
        store.RoomCount.Should().Be(0);
        store.MessageCount.Should().Be(0);
    }

    [Fact]
    public void ConcurrentUserAdditions_ShouldBeThreadSafe()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var taskCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < taskCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var user = new User
                {
                    Nickname = $"user{index}",
                    RegisteredAt = DateTime.UtcNow,
                    IsOnline = true
                };
                store.TryAddUser($"user{index}", user);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        store.UserCount.Should().Be(taskCount);
    }

    [Fact]
    public void ConcurrentMessageAdditions_ShouldBeThreadSafe()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var roomId = Guid.NewGuid();
        var messageCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    RoomId = roomId,
                    Author = "alice",
                    Content = $"Message {index}",
                    Type = MessageType.UserMessage,
                    Timestamp = DateTime.UtcNow
                };
                store.AddMessage(roomId, message);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var messages = store.GetRoomMessages(roomId).ToList();
        messages.Should().HaveCount(messageCount);
    }

    [Fact]
    public void GetRoomMessages_WithLimit_ShouldReturnLastNMessages()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var roomId = Guid.NewGuid();

        for (int i = 0; i < 10; i++)
        {
            var message = new Message
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                Author = "alice",
                Content = $"Message {i}",
                Type = MessageType.UserMessage,
                Timestamp = DateTime.UtcNow.AddSeconds(i)
            };
            store.AddMessage(roomId, message);
        }

        // Act
        var lastFive = store.GetRoomMessages(roomId, 5).ToList();

        // Assert
        lastFive.Should().HaveCount(5);
        lastFive[0].Content.Should().Be("Message 5");
        lastFive[4].Content.Should().Be("Message 9");
    }

    [Fact]
    public void NicknameComparison_ShouldBeCaseInsensitive()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var user = new User { Nickname = "Alice", RegisteredAt = DateTime.UtcNow };

        // Act
        store.TryAddUser("Alice", user);

        // Assert
        store.TryGetUser("alice", out var retrievedUser).Should().BeTrue();
        store.TryGetUser("ALICE", out retrievedUser).Should().BeTrue();
        store.UserExists("aLiCe").Should().BeTrue();
    }
}
