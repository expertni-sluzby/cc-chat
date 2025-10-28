namespace ChatServer.Models;

/// <summary>
/// Represents a chat room in the system
/// </summary>
public class ChatRoom
{
    /// <summary>
    /// Unique identifier for the room
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the room (3-50 characters)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the room (max 200 characters)
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Nickname of the user who created the room
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the room was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// List of nicknames of users currently in the room
    /// </summary>
    public List<string> Participants { get; set; } = new();
}
