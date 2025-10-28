namespace ChatServer.Models.DTOs;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterUserRequest
{
    /// <summary>
    /// User's nickname (3-20 characters, alphanumeric and underscore only)
    /// </summary>
    public string Nickname { get; set; } = string.Empty;
}
