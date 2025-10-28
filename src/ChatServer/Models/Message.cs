namespace ChatServer.Models;

/// <summary>
/// Represents a message in a chat room
/// </summary>
public class Message
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public DateTime Timestamp { get; set; }
}
