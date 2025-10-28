namespace ChatServer.Models.DTOs;

/// <summary>
/// Request to leave a chat room
/// </summary>
public class LeaveRoomRequest
{
    /// <summary>
    /// Nickname of the user leaving the room
    /// </summary>
    public string Nickname { get; set; } = string.Empty;
}
