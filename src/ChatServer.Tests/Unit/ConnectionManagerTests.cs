using ChatServer.Services;
using FluentAssertions;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Unit tests for ConnectionManager
/// </summary>
public class ConnectionManagerTests
{
    [Fact]
    public async Task AddConnection_SingleConnection_TracksConnection()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act
        await manager.AddConnectionAsync("conn1", "user1");

        // Assert
        var nickname = await manager.GetNicknameByConnectionIdAsync("conn1");
        nickname.Should().Be("user1");
    }

    [Fact]
    public async Task AddConnection_MultipleConnectionsPerUser_TracksAll()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act
        await manager.AddConnectionAsync("conn1", "user1");
        await manager.AddConnectionAsync("conn2", "user1");
        await manager.AddConnectionAsync("conn3", "user1");

        // Assert
        var connections = await manager.GetConnectionsByNicknameAsync("user1");
        connections.Should().HaveCount(3);
        connections.Should().Contain(new[] { "conn1", "conn2", "conn3" });
    }

    [Fact]
    public async Task RemoveConnection_ExistingConnection_RemovesSuccessfully()
    {
        // Arrange
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("conn1", "user1");

        // Act
        await manager.RemoveConnectionAsync("conn1");

        // Assert
        var nickname = await manager.GetNicknameByConnectionIdAsync("conn1");
        nickname.Should().BeNull();
    }

    [Fact]
    public async Task RemoveConnection_LastConnectionForUser_RemovesUserEntry()
    {
        // Arrange
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("conn1", "user1");

        // Act
        await manager.RemoveConnectionAsync("conn1");

        // Assert
        var hasConnections = await manager.HasConnectionsAsync("user1");
        hasConnections.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveConnection_OneOfMultiple_KeepsOthers()
    {
        // Arrange
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("conn1", "user1");
        await manager.AddConnectionAsync("conn2", "user1");

        // Act
        await manager.RemoveConnectionAsync("conn1");

        // Assert
        var connections = await manager.GetConnectionsByNicknameAsync("user1");
        connections.Should().HaveCount(1);
        connections.Should().Contain("conn2");

        var hasConnections = await manager.HasConnectionsAsync("user1");
        hasConnections.Should().BeTrue();
    }

    [Fact]
    public async Task GetNicknameByConnectionId_NonExistentConnection_ReturnsNull()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act
        var nickname = await manager.GetNicknameByConnectionIdAsync("nonexistent");

        // Assert
        nickname.Should().BeNull();
    }

    [Fact]
    public async Task GetConnectionsByNickname_NoConnections_ReturnsEmpty()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act
        var connections = await manager.GetConnectionsByNicknameAsync("nonexistent");

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task HasConnections_UserWithConnections_ReturnsTrue()
    {
        // Arrange
        var manager = new ConnectionManager();
        await manager.AddConnectionAsync("conn1", "user1");

        // Act
        var hasConnections = await manager.HasConnectionsAsync("user1");

        // Assert
        hasConnections.Should().BeTrue();
    }

    [Fact]
    public async Task HasConnections_UserWithoutConnections_ReturnsFalse()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act
        var hasConnections = await manager.HasConnectionsAsync("user1");

        // Assert
        hasConnections.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentOperations_MultipleUsers_AllTracked()
    {
        // Arrange
        var manager = new ConnectionManager();

        // Act - Add connections for multiple users concurrently
        var tasks = new List<Task>
        {
            manager.AddConnectionAsync("conn1", "user1"),
            manager.AddConnectionAsync("conn2", "user2"),
            manager.AddConnectionAsync("conn3", "user1"),
            manager.AddConnectionAsync("conn4", "user3"),
            manager.AddConnectionAsync("conn5", "user2")
        };

        await Task.WhenAll(tasks);

        // Assert
        var user1Connections = await manager.GetConnectionsByNicknameAsync("user1");
        var user2Connections = await manager.GetConnectionsByNicknameAsync("user2");
        var user3Connections = await manager.GetConnectionsByNicknameAsync("user3");

        user1Connections.Should().HaveCount(2);
        user2Connections.Should().HaveCount(2);
        user3Connections.Should().HaveCount(1);
    }
}
