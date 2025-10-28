namespace ChatServer.Models.DTOs;

/// <summary>
/// Basic room information response
/// </summary>
public class RoomResponse
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
    /// Number of participants currently in the room
    /// </summary>
    public int ParticipantCount { get; set; }
}
