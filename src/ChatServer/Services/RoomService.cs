using ChatServer.Models;
using ChatServer.Storage;

namespace ChatServer.Services;

/// <summary>
/// Service for managing chat rooms
/// </summary>
public class RoomService : IRoomService
{
    private readonly IDataStore _dataStore;
    private readonly ILogger<RoomService> _logger;
    private readonly IMessageService? _messageService;

    public RoomService(IDataStore dataStore, ILogger<RoomService> logger, IMessageService? messageService = null)
    {
        _dataStore = dataStore;
        _logger = logger;
        _messageService = messageService;
    }

    public Task<ChatRoom?> CreateRoomAsync(string name, string description, string createdBy)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 50)
        {
            _logger.LogWarning("Room creation failed: Invalid name length. Name: {Name}", name);
            return Task.FromResult<ChatRoom?>(null);
        }

        if (description.Length > 200)
        {
            _logger.LogWarning("Room creation failed: Description too long. Length: {Length}", description.Length);
            return Task.FromResult<ChatRoom?>(null);
        }

        // Check if user exists
        if (!_dataStore.UserExists(createdBy))
        {
            _logger.LogWarning("Room creation failed: User not found. User: {User}", createdBy);
            return Task.FromResult<ChatRoom?>(null);
        }

        // Create new room
        var room = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<string>()
        };

        // Add to store
        var success = _dataStore.TryAddRoom(room.Id, room);

        if (success)
        {
            _logger.LogInformation("Room created. RoomId: {RoomId}, Name: {Name}, Creator: {Creator}",
                room.Id, room.Name, createdBy);
        }

        return Task.FromResult(success ? room : null);
    }

    public Task<IEnumerable<ChatRoom>> GetAllRoomsAsync()
    {
        var rooms = _dataStore.GetAllRooms();
        return Task.FromResult(rooms);
    }

    public Task<ChatRoom?> GetRoomByIdAsync(Guid roomId)
    {
        _dataStore.TryGetRoom(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(Guid roomId, string requestingUser)
    {
        // Get room
        if (!_dataStore.TryGetRoom(roomId, out var room))
        {
            _logger.LogWarning("Room deletion failed: Room not found. RoomId: {RoomId}", roomId);
            return Task.FromResult(false);
        }

        // Check if requesting user is the creator
        if (!string.Equals(room!.CreatedBy, requestingUser, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Room deletion failed: User is not creator. RoomId: {RoomId}, User: {User}, Creator: {Creator}",
                roomId, requestingUser, room.CreatedBy);
            return Task.FromResult(false);
        }

        // Remove all participants' current room reference
        foreach (var participant in room.Participants.ToList())
        {
            if (_dataStore.TryGetUser(participant, out var user) && user != null)
            {
                user.CurrentRoomId = null;
                _dataStore.TryUpdateUser(participant, user);
            }
        }

        // Delete room
        var success = _dataStore.TryRemoveRoom(roomId);

        if (success)
        {
            _logger.LogInformation("Room deleted. RoomId: {RoomId}, Name: {Name}, DeletedBy: {User}",
                roomId, room.Name, requestingUser);
        }

        return Task.FromResult(success);
    }

    public async Task<bool> JoinRoomAsync(Guid roomId, string nickname)
    {
        // Check if room exists
        if (!_dataStore.TryGetRoom(roomId, out var room))
        {
            _logger.LogWarning("Join room failed: Room not found. RoomId: {RoomId}, User: {User}", roomId, nickname);
            return false;
        }

        // Check if user exists
        if (!_dataStore.TryGetUser(nickname, out var user) || user == null)
        {
            _logger.LogWarning("Join room failed: User not found. User: {User}", nickname);
            return false;
        }

        // If user is already in this room, do nothing
        if (user.CurrentRoomId == roomId)
        {
            return true;
        }

        // If user is in another room, leave it first
        if (user.CurrentRoomId.HasValue)
        {
            var oldRoomId = user.CurrentRoomId.Value;
            await LeaveRoomAsync(oldRoomId, nickname);
            _logger.LogInformation("User automatically left previous room. User: {User}, OldRoomId: {OldRoomId}",
                nickname, oldRoomId);
        }

        // Add to participants
        var added = _dataStore.AddParticipant(roomId, nickname);

        if (added)
        {
            // Update user's current room
            user.CurrentRoomId = roomId;
            _dataStore.TryUpdateUser(nickname, user);

            _logger.LogInformation("User joined room. User: {User}, RoomId: {RoomId}, RoomName: {RoomName}",
                nickname, roomId, room!.Name);

            // Create system message
            if (_messageService != null)
            {
                await _messageService.CreateSystemMessageAsync(roomId, MessageType.UserJoined, nickname);
            }
        }

        return added;
    }

    public async Task<bool> LeaveRoomAsync(Guid roomId, string nickname)
    {
        // Check if room exists
        if (!_dataStore.TryGetRoom(roomId, out var room))
        {
            _logger.LogWarning("Leave room failed: Room not found. RoomId: {RoomId}, User: {User}", roomId, nickname);
            return false;
        }

        // Check if user exists
        if (!_dataStore.TryGetUser(nickname, out var user) || user == null)
        {
            _logger.LogWarning("Leave room failed: User not found. User: {User}", nickname);
            return false;
        }

        // Remove from participants
        var removed = _dataStore.RemoveParticipant(roomId, nickname);

        if (removed)
        {
            // Update user's current room to null
            user.CurrentRoomId = null;
            _dataStore.TryUpdateUser(nickname, user);

            _logger.LogInformation("User left room. User: {User}, RoomId: {RoomId}, RoomName: {RoomName}",
                nickname, roomId, room!.Name);

            // Create system message
            if (_messageService != null)
            {
                await _messageService.CreateSystemMessageAsync(roomId, MessageType.UserLeft, nickname);
            }
        }

        return removed;
    }

    public Task<bool> IsUserInRoomAsync(Guid roomId, string nickname)
    {
        if (!_dataStore.TryGetRoom(roomId, out var room))
        {
            return Task.FromResult(false);
        }

        var isInRoom = room!.Participants.Contains(nickname, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(isInRoom);
    }

    public Task<Guid?> GetUserCurrentRoomAsync(string nickname)
    {
        if (!_dataStore.TryGetUser(nickname, out var user) || user == null)
        {
            return Task.FromResult<Guid?>(null);
        }

        return Task.FromResult(user.CurrentRoomId);
    }
}
