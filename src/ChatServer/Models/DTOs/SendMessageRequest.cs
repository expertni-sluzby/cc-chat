namespace ChatServer.Models.DTOs;

/// <summary>
/// Request to send a message to a room
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Nickname of the user sending the message
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Message content (max 1000 characters)
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
