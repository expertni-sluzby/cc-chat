using ChatServer.Models;

namespace ChatServer.Storage;

/// <summary>
/// Interface for data storage operations
/// </summary>
public interface IDataStore
{
    #region Users

    /// <summary>
    /// Tries to add a new user to the store
    /// </summary>
    bool TryAddUser(string nickname, User user);

    /// <summary>
    /// Tries to get a user by nickname
    /// </summary>
    bool TryGetUser(string nickname, out User? user);

    /// <summary>
    /// Gets all users from the store
    /// </summary>
    IEnumerable<User> GetAllUsers();

    /// <summary>
    /// Checks if a user with the given nickname exists
    /// </summary>
    bool UserExists(string nickname);

    /// <summary>
    /// Tries to update an existing user
    /// </summary>
    bool TryUpdateUser(string nickname, User user);

    #endregion

    #region Rooms

    /// <summary>
    /// Tries to add a new room to the store
    /// </summary>
    bool TryAddRoom(Guid id, ChatRoom room);

    /// <summary>
    /// Tries to get a room by ID
    /// </summary>
    bool TryGetRoom(Guid id, out ChatRoom? room);

    /// <summary>
    /// Gets all rooms from the store
    /// </summary>
    IEnumerable<ChatRoom> GetAllRooms();

    /// <summary>
    /// Tries to remove a room from the store
    /// </summary>
    bool TryRemoveRoom(Guid id);

    /// <summary>
    /// Checks if a room with the given ID exists
    /// </summary>
    bool RoomExists(Guid id);

    /// <summary>
    /// Adds a participant to a room
    /// </summary>
    bool AddParticipant(Guid roomId, string nickname);

    /// <summary>
    /// Removes a participant from a room
    /// </summary>
    bool RemoveParticipant(Guid roomId, string nickname);

    #endregion

    #region Messages

    /// <summary>
    /// Adds a message to a room
    /// </summary>
    void AddMessage(Guid roomId, Message message);

    /// <summary>
    /// Gets all messages for a room
    /// </summary>
    IEnumerable<Message> GetRoomMessages(Guid roomId);

    /// <summary>
    /// Gets the last N messages for a room
    /// </summary>
    IEnumerable<Message> GetRoomMessages(Guid roomId, int limit);

    #endregion

    #region Utility

    /// <summary>
    /// Clears all data from the store
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the count of users
    /// </summary>
    int UserCount { get; }

    /// <summary>
    /// Gets the count of rooms
    /// </summary>
    int RoomCount { get; }

    /// <summary>
    /// Gets the total count of messages across all rooms
    /// </summary>
    int MessageCount { get; }

    #endregion
}
