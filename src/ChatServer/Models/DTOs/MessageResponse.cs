namespace ChatServer.Models.DTOs;

/// <summary>
/// Response model for message data
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Room ID where the message was sent
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Author of the message (nickname or "SYSTEM")
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of message
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// When the message was sent
    /// </summary>
    public DateTime Timestamp { get; set; }
}
