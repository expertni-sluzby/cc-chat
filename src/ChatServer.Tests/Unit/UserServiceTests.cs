using ChatServer.Models;
using ChatServer.Services;
using ChatServer.Storage;
using FluentAssertions;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Unit tests for UserService
/// </summary>
public class UserServiceTests
{
    private UserService CreateUserService()
    {
        var dataStore = new InMemoryDataStore();
        return new UserService(dataStore);
    }

    [Fact]
    public async Task RegisterUser_ValidNickname_ReturnsUser()
    {
        // Arrange
        var service = CreateUserService();
        var nickname = "testuser";

        // Act
        var result = await service.RegisterUserAsync(nickname);

        // Assert
        result.Should().NotBeNull();
        result!.Nickname.Should().Be(nickname);
        result.IsOnline.Should().BeFalse();
        result.RegisteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterUser_DuplicateNickname_ReturnsNull()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("testuser");

        // Act
        var result = await service.RegisterUserAsync("testuser");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterUser_CaseInsensitive_PreventsConflict()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("TestUser");

        // Act
        var result = await service.RegisterUserAsync("testuser");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("123456789012345678901")] // too long (21 chars)
    [InlineData("user-name")] // invalid char (dash)
    [InlineData("user.name")] // invalid char (dot)
    [InlineData("")] // empty
    [InlineData("user name")] // space
    public async Task RegisterUser_InvalidNickname_ReturnsNull(string nickname)
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.RegisterUserAsync(nickname);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")] // exactly 3 chars
    [InlineData("12345678901234567890")] // exactly 20 chars
    [InlineData("User_123")] // valid with underscore
    [InlineData("test_user_123")] // valid complex
    public async Task RegisterUser_ValidNickname_Success(string nickname)
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.RegisterUserAsync(nickname);

        // Assert
        result.Should().NotBeNull();
        result!.Nickname.Should().Be(nickname);
    }

    [Fact]
    public async Task LoginUser_ExistingUser_SetsOnlineAndReturnsUser()
    {
        // Arrange
        var service = CreateUserService();
        var nickname = "testuser";
        await service.RegisterUserAsync(nickname);

        // Act
        var result = await service.LoginUserAsync(nickname);

        // Assert
        result.Should().NotBeNull();
        result!.Nickname.Should().Be(nickname);
        result.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task LoginUser_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.LoginUserAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginUser_CaseInsensitive_FindsUser()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("TestUser");

        // Act
        var result = await service.LoginUserAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result!.Nickname.Should().Be("TestUser"); // Original casing preserved
        result.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllRegisteredUsers()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("user1");
        await service.RegisterUserAsync("user2");
        await service.RegisterUserAsync("user3");

        // Act
        var result = await service.GetAllUsersAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(u => u.Nickname).Should().Contain(new[] { "user1", "user2", "user3" });
    }

    [Fact]
    public async Task GetAllUsers_EmptyStore_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.GetAllUsersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByNickname_ExistingUser_ReturnsUser()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("testuser");

        // Act
        var result = await service.GetUserByNicknameAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result!.Nickname.Should().Be("testuser");
    }

    [Fact]
    public async Task GetUserByNickname_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.GetUserByNicknameAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IsNicknameAvailable_NewNickname_ReturnsTrue()
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var result = await service.IsNicknameAvailableAsync("newuser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsNicknameAvailable_ExistingNickname_ReturnsFalse()
    {
        // Arrange
        var service = CreateUserService();
        await service.RegisterUserAsync("testuser");

        // Act
        var result = await service.IsNicknameAvailableAsync("testuser");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterUser_ConcurrentCalls_OnlyOneSucceeds()
    {
        // Arrange
        var service = CreateUserService();
        var nickname = "testuser";
        var taskCount = 10;
        var tasks = new List<Task<User?>>();

        // Act - Try to register same nickname concurrently
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(() => service.RegisterUserAsync(nickname)));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var successfulRegistrations = results.Count(r => r != null);
        successfulRegistrations.Should().Be(1, "only one registration should succeed");

        var failedRegistrations = results.Count(r => r == null);
        failedRegistrations.Should().Be(taskCount - 1, "all other registrations should fail");
    }

    [Fact]
    public async Task RegisterUser_PreservesCasing_ButComparesInsensitive()
    {
        // Arrange
        var service = CreateUserService();

        // Act
        var user1 = await service.RegisterUserAsync("TestUser");
        var user2 = await service.RegisterUserAsync("testuser");

        // Assert
        user1.Should().NotBeNull();
        user1!.Nickname.Should().Be("TestUser"); // Original casing preserved
        user2.Should().BeNull(); // Should fail due to case-insensitive comparison
    }
}
