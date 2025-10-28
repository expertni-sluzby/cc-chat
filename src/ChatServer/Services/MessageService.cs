using System.Net;
using ChatServer.Models;
using ChatServer.Storage;

namespace ChatServer.Services;

/// <summary>
/// Service for managing chat messages
/// </summary>
public class MessageService : IMessageService
{
    private readonly IDataStore _dataStore;
    private readonly ILogger<MessageService> _logger;

    public MessageService(IDataStore dataStore, ILogger<MessageService> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    public async Task<Message?> SendMessageAsync(Guid roomId, string author, string content)
    {
        // Validate content
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Send message failed: Empty content. RoomId: {RoomId}, Author: {Author}",
                roomId, author);
            return null;
        }

        content = content.Trim();

        if (content.Length > 1000)
        {
            _logger.LogWarning("Send message failed: Content too long. RoomId: {RoomId}, Author: {Author}, Length: {Length}",
                roomId, author, content.Length);
            return null;
        }

        // Check if room exists and if author is in the room
        if (!_dataStore.TryGetRoom(roomId, out var room))
        {
            _logger.LogWarning("Send message failed: Room not found. RoomId: {RoomId}, Author: {Author}",
                roomId, author);
            return null;
        }

        var isInRoom = room!.Participants.Contains(author, StringComparer.OrdinalIgnoreCase);
        if (!isInRoom)
        {
            _logger.LogWarning("Send message failed: User not in room. RoomId: {RoomId}, Author: {Author}",
                roomId, author);
            return null;
        }

        // Create message
        var message = new Message
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Author = author,
            Content = WebUtility.HtmlEncode(content), // Basic XSS protection
            Type = MessageType.UserMessage,
            Timestamp = DateTime.UtcNow
        };

        // Store message
        _dataStore.AddMessage(roomId, message);

        _logger.LogInformation("Message sent. MessageId: {MessageId}, RoomId: {RoomId}, Author: {Author}",
            message.Id, roomId, author);

        return message;
    }

    public Task<IEnumerable<Message>> GetRoomMessagesAsync(Guid roomId, int? limit = null)
    {
        IEnumerable<Message> messages;

        if (limit.HasValue && limit.Value > 0)
        {
            messages = _dataStore.GetRoomMessages(roomId, limit.Value);
        }
        else
        {
            messages = _dataStore.GetRoomMessages(roomId);
        }

        // Messages are stored in chronological order, just return them
        return Task.FromResult(messages);
    }

    public Task<Message> CreateSystemMessageAsync(Guid roomId, MessageType type, string username)
    {
        if (type != MessageType.UserJoined && type != MessageType.UserLeft)
        {
            throw new ArgumentException($"Invalid system message type: {type}", nameof(type));
        }

        // Generate content based on message type
        var content = type switch
        {
            MessageType.UserJoined => $"{username} vstoupil do místnosti",
            MessageType.UserLeft => $"{username} opustil místnost",
            _ => throw new ArgumentException($"Unsupported message type: {type}")
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Author = "SYSTEM",
            Content = content,
            Type = type,
            Timestamp = DateTime.UtcNow
        };

        // Store message
        _dataStore.AddMessage(roomId, message);

        _logger.LogInformation("System message created. MessageId: {MessageId}, RoomId: {RoomId}, Type: {Type}, User: {User}",
            message.Id, roomId, type, username);

        return Task.FromResult(message);
    }
}
