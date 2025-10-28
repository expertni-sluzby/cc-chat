namespace ChatServer.Models.DTOs;

/// <summary>
/// Request model for user login
/// </summary>
public class LoginUserRequest
{
    /// <summary>
    /// User's nickname
    /// </summary>
    public string Nickname { get; set; } = string.Empty;
}
