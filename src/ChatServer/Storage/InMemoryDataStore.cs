using System.Collections.Concurrent;
using ChatServer.Models;

namespace ChatServer.Storage;

/// <summary>
/// Thread-safe in-memory data store for chat server
/// </summary>
public class InMemoryDataStore : IDataStore
{
    private readonly ConcurrentDictionary<string, User> _users;
    private readonly ConcurrentDictionary<Guid, ChatRoom> _rooms;
    private readonly ConcurrentDictionary<Guid, List<Message>> _messages;
    private readonly object _messageLock = new();
    private readonly object _roomLock = new();

    public InMemoryDataStore()
    {
        _users = new ConcurrentDictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        _rooms = new ConcurrentDictionary<Guid, ChatRoom>();
        _messages = new ConcurrentDictionary<Guid, List<Message>>();
    }

    #region Users

    public bool TryAddUser(string nickname, User user)
    {
        return _users.TryAdd(nickname, user);
    }

    public bool TryGetUser(string nickname, out User? user)
    {
        return _users.TryGetValue(nickname, out user);
    }

    public IEnumerable<User> GetAllUsers()
    {
        return _users.Values.ToList();
    }

    public bool UserExists(string nickname)
    {
        return _users.ContainsKey(nickname);
    }

    public bool TryUpdateUser(string nickname, User user)
    {
        if (!_users.ContainsKey(nickname))
            return false;

        _users[nickname] = user;
        return true;
    }

    #endregion

    #region Rooms

    public bool TryAddRoom(Guid id, ChatRoom room)
    {
        var added = _rooms.TryAdd(id, room);
        if (added)
        {
            // Initialize empty message list for the room
            _messages.TryAdd(id, new List<Message>());
        }
        return added;
    }

    public bool TryGetRoom(Guid id, out ChatRoom? room)
    {
        return _rooms.TryGetValue(id, out room);
    }

    public IEnumerable<ChatRoom> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    public bool TryRemoveRoom(Guid id)
    {
        var removed = _rooms.TryRemove(id, out _);
        if (removed)
        {
            // Also remove messages for this room
            _messages.TryRemove(id, out _);
        }
        return removed;
    }

    public bool RoomExists(Guid id)
    {
        return _rooms.ContainsKey(id);
    }

    public bool AddParticipant(Guid roomId, string nickname)
    {
        lock (_roomLock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            if (room.Participants.Contains(nickname, StringComparer.OrdinalIgnoreCase))
                return false; // Already in room

            room.Participants.Add(nickname);
            return true;
        }
    }

    public bool RemoveParticipant(Guid roomId, string nickname)
    {
        lock (_roomLock)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return false;

            var removed = room.Participants.RemoveAll(p =>
                string.Equals(p, nickname, StringComparison.OrdinalIgnoreCase)) > 0;

            return removed;
        }
    }

    #endregion

    #region Messages

    public void AddMessage(Guid roomId, Message message)
    {
        lock (_messageLock)
        {
            if (_messages.TryGetValue(roomId, out var messageList))
            {
                messageList.Add(message);
            }
            else
            {
                _messages.TryAdd(roomId, new List<Message> { message });
            }
        }
    }

    public IEnumerable<Message> GetRoomMessages(Guid roomId)
    {
        lock (_messageLock)
        {
            if (_messages.TryGetValue(roomId, out var messageList))
            {
                return messageList.ToList(); // Return a copy
            }
            return Enumerable.Empty<Message>();
        }
    }

    public IEnumerable<Message> GetRoomMessages(Guid roomId, int limit)
    {
        lock (_messageLock)
        {
            if (_messages.TryGetValue(roomId, out var messageList))
            {
                return messageList.TakeLast(limit).ToList();
            }
            return Enumerable.Empty<Message>();
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Clears all data from the store (useful for testing)
    /// </summary>
    public void Clear()
    {
        _users.Clear();
        _rooms.Clear();
        _messages.Clear();
    }

    /// <summary>
    /// Gets the count of users
    /// </summary>
    public int UserCount => _users.Count;

    /// <summary>
    /// Gets the count of rooms
    /// </summary>
    public int RoomCount => _rooms.Count;

    /// <summary>
    /// Gets the total count of messages across all rooms
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_messageLock)
            {
                return _messages.Values.Sum(list => list.Count);
            }
        }
    }

    #endregion
}
