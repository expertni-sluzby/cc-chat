using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ChatServer.Models.DTOs;
using ChatServer.Services;
using ChatServer.Hubs;

namespace ChatServer.Controllers;

/// <summary>
/// Controller for room management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomService roomService, IHubContext<ChatHub> hubContext, ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new chat room
    /// </summary>
    /// <param name="request">Room creation request</param>
    /// <returns>Created room information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        _logger.LogInformation("Attempting to create room: {Name} by {Creator}",
            request.Name, request.CreatedBy);

        var room = await _roomService.CreateRoomAsync(
            request.Name,
            request.Description,
            request.CreatedBy);

        if (room == null)
        {
            _logger.LogWarning("Room creation failed: {Name} by {Creator}",
                request.Name, request.CreatedBy);
            return BadRequest(new ErrorResponse
            {
                Error = "Room creation failed",
                Details = "Invalid room data or user not found"
            });
        }

        _logger.LogInformation("Room created successfully: {RoomId}, {Name}", room.Id, room.Name);

        var response = new RoomResponse
        {
            Id = room.Id,
            Name = room.Name,
            Description = room.Description,
            CreatedBy = room.CreatedBy,
            CreatedAt = room.CreatedAt,
            ParticipantCount = room.Participants.Count
        };

        // Broadcast room created event to all connected clients
        await _hubContext.Clients.All.SendAsync("RoomCreated", response);

        return CreatedAtAction(nameof(GetRoomById), new { id = room.Id }, response);
    }

    /// <summary>
    /// Gets all rooms
    /// </summary>
    /// <returns>List of all rooms</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRooms()
    {
        _logger.LogInformation("Retrieving all rooms");

        var rooms = await _roomService.GetAllRoomsAsync();

        var response = rooms.Select(r => new RoomResponse
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            CreatedBy = r.CreatedBy,
            CreatedAt = r.CreatedAt,
            ParticipantCount = r.Participants.Count
        });

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific room by ID
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <returns>Detailed room information including participants</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoomDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomById(Guid id)
    {
        _logger.LogInformation("Retrieving room: {RoomId}", id);

        var room = await _roomService.GetRoomByIdAsync(id);

        if (room == null)
        {
            _logger.LogWarning("Room not found: {RoomId}", id);
            return NotFound(new ErrorResponse
            {
                Error = "Room not found",
                Details = $"No room found with ID '{id}'"
            });
        }

        var response = new RoomDetailResponse
        {
            Id = room.Id,
            Name = room.Name,
            Description = room.Description,
            CreatedBy = room.CreatedBy,
            CreatedAt = room.CreatedAt,
            Participants = room.Participants
        };

        return Ok(response);
    }

    /// <summary>
    /// Deletes a room (only by creator)
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="requestingUser">Nickname of user requesting deletion</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoom(Guid id, [FromQuery] string requestingUser)
    {
        _logger.LogInformation("Attempting to delete room: {RoomId} by {User}", id, requestingUser);

        if (string.IsNullOrWhiteSpace(requestingUser))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Missing requesting user",
                Details = "requestingUser query parameter is required"
            });
        }

        var room = await _roomService.GetRoomByIdAsync(id);
        if (room == null)
        {
            _logger.LogWarning("Delete failed - room not found: {RoomId}", id);
            return NotFound(new ErrorResponse
            {
                Error = "Room not found",
                Details = $"No room found with ID '{id}'"
            });
        }

        var deleted = await _roomService.DeleteRoomAsync(id, requestingUser);

        if (!deleted)
        {
            _logger.LogWarning("Delete failed - user is not creator: {RoomId}, {User}", id, requestingUser);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                Error = "Forbidden",
                Details = "Only the room creator can delete the room"
            });
        }

        _logger.LogInformation("Room deleted successfully: {RoomId}", id);

        // Broadcast room deleted event to all connected clients
        await _hubContext.Clients.All.SendAsync("RoomDeleted", id.ToString());

        return NoContent();
    }

    /// <summary>
    /// User joins a room
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="request">Join request with user nickname</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/join")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinRoom(Guid id, [FromBody] JoinRoomRequest request)
    {
        _logger.LogInformation("User {Nickname} attempting to join room {RoomId}",
            request.Nickname, id);

        var success = await _roomService.JoinRoomAsync(id, request.Nickname);

        if (!success)
        {
            _logger.LogWarning("Join room failed: {RoomId}, {User}", id, request.Nickname);
            return NotFound(new ErrorResponse
            {
                Error = "Join room failed",
                Details = "Room or user not found"
            });
        }

        _logger.LogInformation("User {Nickname} joined room {RoomId}", request.Nickname, id);
        return Ok(new { message = "Successfully joined room" });
    }

    /// <summary>
    /// User leaves a room
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="request">Leave request with user nickname</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/leave")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveRoom(Guid id, [FromBody] LeaveRoomRequest request)
    {
        _logger.LogInformation("User {Nickname} attempting to leave room {RoomId}",
            request.Nickname, id);

        var success = await _roomService.LeaveRoomAsync(id, request.Nickname);

        if (!success)
        {
            _logger.LogWarning("Leave room failed: {RoomId}, {User}", id, request.Nickname);
            return BadRequest(new ErrorResponse
            {
                Error = "Leave room failed",
                Details = "Room not found, user not found, or user not in room"
            });
        }

        _logger.LogInformation("User {Nickname} left room {RoomId}", request.Nickname, id);
        return Ok(new { message = "Successfully left room" });
    }
}
