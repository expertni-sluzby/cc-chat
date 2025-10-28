namespace ChatServer.Models.DTOs;

/// <summary>
/// Response model for user data
/// </summary>
public class UserResponse
{
    /// <summary>
    /// User's nickname
    /// </summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// Registration timestamp
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Online status
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Current room ID if user is in a room
    /// </summary>
    public Guid? CurrentRoomId { get; set; }
}
