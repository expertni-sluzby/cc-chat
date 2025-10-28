namespace ChatServer.Models.DTOs;

/// <summary>
/// Request to join a chat room
/// </summary>
public class JoinRoomRequest
{
    /// <summary>
    /// Nickname of the user joining the room
    /// </summary>
    public string Nickname { get; set; } = string.Empty;
}
