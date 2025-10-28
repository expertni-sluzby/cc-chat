using ChatServer.Models;

namespace ChatServer.Services;

/// <summary>
/// Service for managing chat messages
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a user message to a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="author">Author's nickname</param>
    /// <param name="content">Message content (max 1000 characters)</param>
    /// <returns>The created message, or null if validation fails or user not in room</returns>
    Task<Message?> SendMessageAsync(Guid roomId, string author, string content);

    /// <summary>
    /// Gets messages from a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="limit">Optional limit on number of messages (most recent first)</param>
    /// <returns>Messages ordered chronologically (oldest first)</returns>
    Task<IEnumerable<Message>> GetRoomMessagesAsync(Guid roomId, int? limit = null);

    /// <summary>
    /// Creates a system message (UserJoined or UserLeft)
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="type">Type of system message</param>
    /// <param name="username">Username for the message</param>
    /// <returns>The created system message</returns>
    Task<Message> CreateSystemMessageAsync(Guid roomId, MessageType type, string username);
}
