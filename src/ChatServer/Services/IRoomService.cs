using ChatServer.Models;

namespace ChatServer.Services;

/// <summary>
/// Service for managing chat rooms
/// </summary>
public interface IRoomService
{
    /// <summary>
    /// Creates a new chat room
    /// </summary>
    /// <param name="name">Room name (3-50 characters)</param>
    /// <param name="description">Room description (max 200 characters)</param>
    /// <param name="createdBy">Nickname of the user creating the room</param>
    /// <returns>The created room, or null if validation fails or user doesn't exist</returns>
    Task<ChatRoom?> CreateRoomAsync(string name, string description, string createdBy);

    /// <summary>
    /// Gets all rooms
    /// </summary>
    Task<IEnumerable<ChatRoom>> GetAllRoomsAsync();

    /// <summary>
    /// Gets a room by ID
    /// </summary>
    Task<ChatRoom?> GetRoomByIdAsync(Guid roomId);

    /// <summary>
    /// Deletes a room (only by creator)
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="requestingUser">Nickname of user requesting deletion</param>
    /// <returns>True if deleted, false if not found or user is not creator</returns>
    Task<bool> DeleteRoomAsync(Guid roomId, string requestingUser);

    /// <summary>
    /// User joins a room (automatically leaves current room if in one)
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="nickname">User nickname</param>
    /// <returns>True if joined, false if room or user doesn't exist</returns>
    Task<bool> JoinRoomAsync(Guid roomId, string nickname);

    /// <summary>
    /// User leaves a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="nickname">User nickname</param>
    /// <returns>True if left, false if not in room or room doesn't exist</returns>
    Task<bool> LeaveRoomAsync(Guid roomId, string nickname);

    /// <summary>
    /// Checks if a user is in a specific room
    /// </summary>
    Task<bool> IsUserInRoomAsync(Guid roomId, string nickname);

    /// <summary>
    /// Gets the current room ID for a user
    /// </summary>
    /// <returns>Room ID if user is in a room, null otherwise</returns>
    Task<Guid?> GetUserCurrentRoomAsync(string nickname);
}
