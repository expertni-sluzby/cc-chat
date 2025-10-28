using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChatServer.Models.DTOs;
using ChatServer.Services;
using ChatServer.Hubs;

namespace ChatServer.Controllers;

/// <summary>
/// Controller for message operations
/// </summary>
[ApiController]
[Route("api/rooms/{roomId}/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IRoomService _roomService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        IRoomService roomService,
        IHubContext<ChatHub> hubContext,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _roomService = roomService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message to a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="request">Message request</param>
    /// <returns>Created message</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(Guid roomId, [FromBody] SendMessageRequest request)
    {
        _logger.LogInformation("Attempting to send message to room: {RoomId} by {Author}",
            roomId, request.Author);

        // Check if room exists
        var room = await _roomService.GetRoomByIdAsync(roomId);
        if (room == null)
        {
            _logger.LogWarning("Send message failed: Room not found. RoomId: {RoomId}", roomId);
            return NotFound(new ErrorResponse
            {
                Error = "Room not found",
                Details = $"No room found with ID '{roomId}'"
            });
        }

        // Send message (service handles validation and permissions)
        var message = await _messageService.SendMessageAsync(roomId, request.Author, request.Content);

        if (message == null)
        {
            // Could be validation error or permission error
            // Check if user is in room to provide better error message
            var isInRoom = await _roomService.IsUserInRoomAsync(roomId, request.Author);
            if (!isInRoom)
            {
                _logger.LogWarning("Send message failed: User not in room. RoomId: {RoomId}, Author: {Author}",
                    roomId, request.Author);
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
                {
                    Error = "Forbidden",
                    Details = "You must be in the room to send messages"
                });
            }

            _logger.LogWarning("Send message failed: Validation error. RoomId: {RoomId}, Author: {Author}",
                roomId, request.Author);
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid message",
                Details = "Message content is invalid or too long (max 1000 characters)"
            });
        }

        _logger.LogInformation("Message sent successfully. MessageId: {MessageId}, RoomId: {RoomId}",
            message.Id, roomId);

        var response = new MessageResponse
        {
            Id = message.Id,
            RoomId = message.RoomId,
            Author = message.Author,
            Content = message.Content,
            Type = message.Type,
            Timestamp = message.Timestamp
        };

        // Broadcast message to WebSocket clients
        await _hubContext.Clients.Group(roomId.ToString())
            .SendAsync("ReceiveMessage", response);

        return CreatedAtAction(nameof(GetMessages), new { roomId }, response);
    }

    /// <summary>
    /// Gets messages from a room
    /// </summary>
    /// <param name="roomId">Room ID</param>
    /// <param name="limit">Optional limit on number of messages</param>
    /// <returns>List of messages</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] int? limit = null)
    {
        _logger.LogInformation("Retrieving messages from room: {RoomId}, Limit: {Limit}",
            roomId, limit);

        // Check if room exists
        var room = await _roomService.GetRoomByIdAsync(roomId);
        if (room == null)
        {
            _logger.LogWarning("Get messages failed: Room not found. RoomId: {RoomId}", roomId);
            return NotFound(new ErrorResponse
            {
                Error = "Room not found",
                Details = $"No room found with ID '{roomId}'"
            });
        }

        // Get messages
        var messages = await _messageService.GetRoomMessagesAsync(roomId, limit);

        var response = messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            RoomId = m.RoomId,
            Author = m.Author,
            Content = m.Content,
            Type = m.Type,
            Timestamp = m.Timestamp
        });

        return Ok(response);
    }
}
