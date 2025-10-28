using ChatServer.Models.DTOs;
using ChatServer.Validators;
using FluentAssertions;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Tests for FluentValidation validators
/// </summary>
public class ValidatorTests
{
    [Theory]
    [InlineData("ab")] // too short
    [InlineData("123456789012345678901")] // too long (21 chars)
    [InlineData("user-name")] // invalid char (dash)
    [InlineData("user.name")] // invalid char (dot)
    [InlineData("")] // empty
    [InlineData("user name")] // space
    [InlineData("user@name")] // special char
    [InlineData("user#name")] // special char
    public async Task RegisterUserRequestValidator_InvalidNickname_FailsValidation(string nickname)
    {
        // Arrange
        var validator = new RegisterUserRequestValidator();
        var request = new RegisterUserRequest { Nickname = nickname };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("abc")] // exactly 3 chars
    [InlineData("12345678901234567890")] // exactly 20 chars
    [InlineData("User_123")] // valid with underscore
    [InlineData("test_user_123")] // valid complex
    [InlineData("validUser")] // simple valid
    [InlineData("user123")] // alphanumeric
    [InlineData("123")] // numbers only
    [InlineData("___")] // underscores only
    public async Task RegisterUserRequestValidator_ValidNickname_PassesValidation(string nickname)
    {
        // Arrange
        var validator = new RegisterUserRequestValidator();
        var request = new RegisterUserRequest { Nickname = nickname };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("123456789012345678901")] // too long (21 chars)
    [InlineData("user-name")] // invalid char
    [InlineData("")] // empty
    public async Task LoginUserRequestValidator_InvalidNickname_FailsValidation(string nickname)
    {
        // Arrange
        var validator = new LoginUserRequestValidator();
        var request = new LoginUserRequest { Nickname = nickname };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("abc")] // exactly 3 chars
    [InlineData("12345678901234567890")] // exactly 20 chars
    [InlineData("User_123")] // valid with underscore
    [InlineData("validUser")] // simple valid
    public async Task LoginUserRequestValidator_ValidNickname_PassesValidation(string nickname)
    {
        // Arrange
        var validator = new LoginUserRequestValidator();
        var request = new LoginUserRequest { Nickname = nickname };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterUserRequestValidator_EmptyNickname_ReturnsSpecificError()
    {
        // Arrange
        var validator = new RegisterUserRequestValidator();
        var request = new RegisterUserRequest { Nickname = "" };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public async Task RegisterUserRequestValidator_TooShortNickname_ReturnsLengthError()
    {
        // Arrange
        var validator = new RegisterUserRequestValidator();
        var request = new RegisterUserRequest { Nickname = "ab" };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("between 3 and 20"));
    }

    [Fact]
    public async Task RegisterUserRequestValidator_InvalidCharacters_ReturnsPatternError()
    {
        // Arrange
        var validator = new RegisterUserRequestValidator();
        var request = new RegisterUserRequest { Nickname = "user-name" };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("letters, numbers, and underscores"));
    }
}
