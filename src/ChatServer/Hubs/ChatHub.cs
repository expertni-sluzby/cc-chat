using ChatServer.Models;
using ChatServer.Models.DTOs;
using ChatServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace ChatServer.Hubs;

/// <summary>
/// SignalR hub for real-time chat communication
/// </summary>
public class ChatHub : Hub
{
    private readonly IUserService _userService;
    private readonly IRoomService _roomService;
    private readonly IMessageService _messageService;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IUserService userService,
        IRoomService roomService,
        IMessageService messageService,
        IConnectionManager connectionManager,
        ILogger<ChatHub> logger)
    {
        _userService = userService;
        _roomService = roomService;
        _messageService = messageService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Client joins a room
    /// </summary>
    /// <param name="roomId">Room ID (as string)</param>
    /// <param name="nickname">User's nickname</param>
    public async Task JoinRoom(string roomId, string nickname)
    {
        _logger.LogInformation("User {Nickname} attempting to join room {RoomId} via WebSocket", nickname, roomId);

        // Parse and validate room ID
        if (!Guid.TryParse(roomId, out var roomGuid))
        {
            _logger.LogWarning("Invalid room ID format: {RoomId}", roomId);
            throw new HubException("Invalid room ID format");
        }

        // Validate user exists
        var user = await _userService.GetUserByNicknameAsync(nickname);
        if (user == null)
        {
            _logger.LogWarning("User not found: {Nickname}", nickname);
            throw new HubException("User not found. Please register first.");
        }

        // Validate room exists
        var room = await _roomService.GetRoomByIdAsync(roomGuid);
        if (room == null)
        {
            _logger.LogWarning("Room not found: {RoomId}", roomId);
            throw new HubException("Room not found");
        }

        // Track connection
        await _connectionManager.AddConnectionAsync(Context.ConnectionId, nickname);

        // Join room (this creates system message via RoomService)
        var success = await _roomService.JoinRoomAsync(roomGuid, nickname);
        if (!success)
        {
            _logger.LogWarning("Failed to join room: {RoomId}, {Nickname}", roomId, nickname);
            throw new HubException("Failed to join room");
        }

        // Add to SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Notify others in room about new user (system message was already sent via RoomService)
        await Clients.OthersInGroup(roomId).SendAsync("UserJoined", nickname, roomId);

        // Send room history to joining user
        var messages = await _messageService.GetRoomMessagesAsync(roomGuid);
        var messageResponses = messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            RoomId = m.RoomId,
            Author = m.Author,
            Content = m.Content,
            Type = m.Type,
            Timestamp = m.Timestamp
        }).ToList();

        await Clients.Caller.SendAsync("RoomHistory", messageResponses);

        _logger.LogInformation("User {Nickname} joined room {RoomId} via WebSocket successfully", nickname, roomId);
    }

    /// <summary>
    /// Client leaves a room
    /// </summary>
    /// <param name="roomId">Room ID (as string)</param>
    /// <param name="nickname">User's nickname</param>
    public async Task LeaveRoom(string roomId, string nickname)
    {
        _logger.LogInformation("User {Nickname} attempting to leave room {RoomId} via WebSocket", nickname, roomId);

        // Parse and validate room ID
        if (!Guid.TryParse(roomId, out var roomGuid))
        {
            _logger.LogWarning("Invalid room ID format: {RoomId}", roomId);
            throw new HubException("Invalid room ID format");
        }

        // Leave room (this creates system message via RoomService)
        var success = await _roomService.LeaveRoomAsync(roomGuid, nickname);
        if (!success)
        {
            _logger.LogWarning("Failed to leave room: {RoomId}, {Nickname}", roomId, nickname);
            throw new HubException("Failed to leave room");
        }

        // Remove from SignalR group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        // Notify others in room that user left
        await Clients.OthersInGroup(roomId).SendAsync("UserLeft", nickname, roomId);

        _logger.LogInformation("User {Nickname} left room {RoomId} via WebSocket successfully", nickname, roomId);
    }

    /// <summary>
    /// Client sends a message to a room
    /// </summary>
    /// <param name="roomId">Room ID (as string)</param>
    /// <param name="content">Message content</param>
    public async Task SendMessage(string roomId, string content)
    {
        // Get nickname from connection
        var nickname = await _connectionManager.GetNicknameByConnectionIdAsync(Context.ConnectionId);
        if (nickname == null)
        {
            _logger.LogWarning("Connection not associated with user: {ConnectionId}", Context.ConnectionId);
            throw new HubException("Connection not authenticated");
        }

        _logger.LogInformation("User {Nickname} sending message to room {RoomId} via WebSocket", nickname, roomId);

        // Parse and validate room ID
        if (!Guid.TryParse(roomId, out var roomGuid))
        {
            _logger.LogWarning("Invalid room ID format: {RoomId}", roomId);
            throw new HubException("Invalid room ID format");
        }

        // Send message (service handles validation)
        var message = await _messageService.SendMessageAsync(roomGuid, nickname, content);
        if (message == null)
        {
            _logger.LogWarning("Failed to send message: {RoomId}, {Nickname}", roomId, nickname);
            throw new HubException("Failed to send message. Check permissions and content.");
        }

        // Broadcast message to all in room (including sender)
        var messageResponse = new MessageResponse
        {
            Id = message.Id,
            RoomId = message.RoomId,
            Author = message.Author,
            Content = message.Content,
            Type = message.Type,
            Timestamp = message.Timestamp
        };

        await Clients.Group(roomId).SendAsync("ReceiveMessage", messageResponse);

        _logger.LogInformation("Message sent successfully: {MessageId}, {RoomId}", message.Id, roomId);
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    /// <param name="exception">Exception that caused disconnect, if any</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);

        // Get nickname before removing connection
        var nickname = await _connectionManager.GetNicknameByConnectionIdAsync(connectionId);

        // Remove connection
        await _connectionManager.RemoveConnectionAsync(connectionId);

        if (nickname != null)
        {
            // Check if user has other active connections
            var hasOtherConnections = await _connectionManager.HasConnectionsAsync(nickname);

            if (!hasOtherConnections)
            {
                _logger.LogInformation("User {Nickname} has no more active connections", nickname);

                // Get user's current room and leave if in one
                var currentRoomId = await _roomService.GetUserCurrentRoomAsync(nickname);
                if (currentRoomId.HasValue)
                {
                    _logger.LogInformation("Auto-leaving room {RoomId} for disconnected user {Nickname}",
                        currentRoomId.Value, nickname);

                    await _roomService.LeaveRoomAsync(currentRoomId.Value, nickname);

                    // Notify others in room
                    await Clients.Group(currentRoomId.Value.ToString()).SendAsync("UserLeft", nickname, currentRoomId.Value.ToString());
                }
            }
            else
            {
                _logger.LogInformation("User {Nickname} still has {Count} active connection(s)",
                    nickname, (await _connectionManager.GetConnectionsByNicknameAsync(nickname)).Count());
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
