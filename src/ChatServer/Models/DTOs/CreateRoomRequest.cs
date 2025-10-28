namespace ChatServer.Models.DTOs;

/// <summary>
/// Request to create a new chat room
/// </summary>
public class CreateRoomRequest
{
    /// <summary>
    /// Name of the room
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the room
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Nickname of the user creating the room
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}
