namespace ChatServer.Models;

/// <summary>
/// Represents a user in the chat system
/// </summary>
public class User
{
    public string Nickname { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public bool IsOnline { get; set; }
    public Guid? CurrentRoomId { get; set; }
}
