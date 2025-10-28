using System.Net;
using System.Net.Http.Json;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChatServer.Tests.Integration;

/// <summary>
/// Integration tests for UsersController
/// </summary>
public class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UsersControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ValidRequest_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueNickname = $"testuser_{Guid.NewGuid():N}".Substring(0, 20); // Unique nickname
        var request = new { nickname = uniqueNickname };

        // Act
        var response = await client.PostAsJsonAsync("/api/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Nickname.Should().Be(uniqueNickname);
        user.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task Register_DuplicateNickname_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueNickname = $"dup_{Guid.NewGuid():N}".Substring(0, 20);
        var request = new { nickname = uniqueNickname };

        // First registration
        await client.PostAsJsonAsync("/api/users/register", request);

        // Act - Try to register same nickname again
        var response = await client.PostAsJsonAsync("/api/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("already exists");
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("123456789012345678901")] // too long
    [InlineData("user-name")] // invalid char
    [InlineData("")] // empty
    public async Task Register_InvalidNickname_Returns400BadRequest(string nickname)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { nickname };

        // Act
        var response = await client.PostAsJsonAsync("/api/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ExistingUser_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var nickname = $"login_{Guid.NewGuid():N}".Substring(0, 20);

        // Register user first
        await client.PostAsJsonAsync("/api/users/register", new { nickname });

        // Act
        var response = await client.PostAsJsonAsync("/api/users/login", new { nickname });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Nickname.Should().Be(nickname);
        user.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task Login_NonExistingUser_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueNickname = $"nonex_{Guid.NewGuid():N}".Substring(0, 20);
        var request = new { nickname = uniqueNickname };

        // Act
        var response = await client.PostAsJsonAsync("/api/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Login_CaseInsensitive_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueBase = Guid.NewGuid().ToString("N").Substring(0, 10);
        var nickname = $"Test{uniqueBase}";

        // Register with mixed case
        await client.PostAsJsonAsync("/api/users/register", new { nickname });

        // Act - Login with lowercase
        var response = await client.PostAsJsonAsync("/api/users/login", new { nickname = nickname.ToLower() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Nickname.Should().Be(nickname); // Original casing preserved
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllRegisteredUsers()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueBase = Guid.NewGuid().ToString("N").Substring(0, 10);
        var user1 = $"u1_{uniqueBase}";
        var user2 = $"u2_{uniqueBase}";
        var user3 = $"u3_{uniqueBase}";

        // Register multiple users
        await client.PostAsJsonAsync("/api/users/register", new { nickname = user1 });
        await client.PostAsJsonAsync("/api/users/register", new { nickname = user2 });
        await client.PostAsJsonAsync("/api/users/register", new { nickname = user3 });

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        users.Should().NotBeNull();
        users!.Count.Should().BeGreaterThanOrEqualTo(3); // At least our 3 users (might have more from other tests)
        users.Select(u => u.Nickname).Should().Contain(new[] { user1, user2, user3 });
    }

    [Fact]
    public async Task Register_PreservesCasing_ButPreventsConflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var uniqueBase = Guid.NewGuid().ToString("N").Substring(0, 10);
        var nickname = $"Test{uniqueBase}";

        // Act - Register with mixed case
        var response1 = await client.PostAsJsonAsync("/api/users/register", new { nickname });
        var user1 = await response1.Content.ReadFromJsonAsync<UserResponse>();

        // Try to register with different casing
        var response2 = await client.PostAsJsonAsync("/api/users/register", new { nickname = nickname.ToLower() });

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        user1!.Nickname.Should().Be(nickname); // Original casing preserved

        response2.StatusCode.Should().Be(HttpStatusCode.Conflict); // Should fail due to case-insensitive comparison
    }
}
