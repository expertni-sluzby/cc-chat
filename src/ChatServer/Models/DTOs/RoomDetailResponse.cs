namespace ChatServer.Models.DTOs;

/// <summary>
/// Detailed room information including participants
/// </summary>
public class RoomDetailResponse
{
    /// <summary>
    /// Unique identifier for the room
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the room
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the room
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
