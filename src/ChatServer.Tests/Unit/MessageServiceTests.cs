using ChatServer.Models;
using ChatServer.Services;
using ChatServer.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChatServer.Tests.Unit;

/// <summary>
/// Unit tests for MessageService
/// </summary>
public class MessageServiceTests
{
    private MessageService CreateMessageService(IDataStore? dataStore = null)
    {
        var store = dataStore ?? new InMemoryDataStore();
        var logger = new Mock<ILogger<MessageService>>().Object;
        return new MessageService(store, logger);
    }

    private UserService CreateUserService(IDataStore dataStore)
    {
        return new UserService(dataStore);
    }

    private RoomService CreateRoomService(IDataStore dataStore, IMessageService? messageService = null)
    {
        var logger = new Mock<ILogger<RoomService>>().Object;
        return new RoomService(dataStore, logger, messageService);
    }

    private async Task<ChatRoom> CreateRoomWithUser(IDataStore dataStore, string nickname)
    {
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);

        await userService.RegisterUserAsync(nickname);
        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");
        await roomService.JoinRoomAsync(room!.Id, nickname);

        return room;
    }

    [Fact]
    public async Task SendMessage_ValidData_ReturnsMessage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", "Hello!");

        // Assert
        message.Should().NotBeNull();
        message!.Content.Should().Be("Hello!");
        message.Author.Should().Be("user1");
        message.Type.Should().Be(MessageType.UserMessage);
        message.RoomId.Should().Be(room.Id);
        message.Id.Should().NotBe(Guid.Empty);
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", "");

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_WhitespaceContent_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", "   ");

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_ContentTooLong_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");
        var longContent = new string('x', 1001); // 1001 characters

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", longContent);

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_ContentWithWhitespace_TrimsContent()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", "  Hello!  ");

        // Assert
        message.Should().NotBeNull();
        message!.Content.Should().Be("Hello!");
    }

    [Fact]
    public async Task SendMessage_RoomNotFound_ReturnsNull()
    {
        // Arrange
        var messageService = CreateMessageService();

        // Act
        var message = await messageService.SendMessageAsync(Guid.NewGuid(), "user1", "Hello!");

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_UserNotInRoom_ReturnsNull()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        var messageService = CreateMessageService(dataStore);

        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("outsider");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");

        // Act - outsider tries to send message without joining
        var message = await messageService.SendMessageAsync(room!.Id, "outsider", "Hello!");

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public async Task SendMessage_ContentWithHtmlSpecialChars_EncodesContent()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var message = await messageService.SendMessageAsync(room.Id, "user1", "<script>alert('xss')</script>");

        // Assert
        message.Should().NotBeNull();
        message!.Content.Should().Contain("&lt;").And.Contain("&gt;");
        message.Content.Should().NotContain("<script>");
    }

    [Fact]
    public async Task GetRoomMessages_NoMessages_ReturnsEmpty()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        // Act
        var messages = await messageService.GetRoomMessagesAsync(room.Id);

        // Assert
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoomMessages_MultipleMessages_ReturnsInChronologicalOrder()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        await messageService.SendMessageAsync(room.Id, "user1", "First");
        await Task.Delay(10);
        await messageService.SendMessageAsync(room.Id, "user1", "Second");
        await Task.Delay(10);
        await messageService.SendMessageAsync(room.Id, "user1", "Third");

        // Act
        var messages = await messageService.GetRoomMessagesAsync(room.Id);

        // Assert
        var list = messages.ToList();
        list.Should().HaveCount(3);
        list[0].Content.Should().Be("First");
        list[1].Content.Should().Be("Second");
        list[2].Content.Should().Be("Third");
    }

    [Fact]
    public async Task GetRoomMessages_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var room = await CreateRoomWithUser(dataStore, "user1");

        await messageService.SendMessageAsync(room.Id, "user1", "First");
        await messageService.SendMessageAsync(room.Id, "user1", "Second");
        await messageService.SendMessageAsync(room.Id, "user1", "Third");

        // Act
        var messages = await messageService.GetRoomMessagesAsync(room.Id, limit: 2);

        // Assert
        var list = messages.ToList();
        list.Should().HaveCount(2);
        // Should return last 2 messages
        list[0].Content.Should().Be("Second");
        list[1].Content.Should().Be("Third");
    }

    [Fact]
    public async Task CreateSystemMessage_UserJoined_CreatesCorrectMessage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        var messageService = CreateMessageService(dataStore);

        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");

        // Act
        var message = await messageService.CreateSystemMessageAsync(
            room!.Id,
            MessageType.UserJoined,
            "user1"
        );

        // Assert
        message.Should().NotBeNull();
        message.Author.Should().Be("SYSTEM");
        message.Content.Should().Contain("user1");
        message.Content.Should().Contain("vstoupil");
        message.Type.Should().Be(MessageType.UserJoined);
        message.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task CreateSystemMessage_UserLeft_CreatesCorrectMessage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);
        var messageService = CreateMessageService(dataStore);

        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");

        // Act
        var message = await messageService.CreateSystemMessageAsync(
            room!.Id,
            MessageType.UserLeft,
            "user1"
        );

        // Assert
        message.Should().NotBeNull();
        message.Author.Should().Be("SYSTEM");
        message.Content.Should().Contain("user1");
        message.Content.Should().Contain("opustil");
        message.Type.Should().Be(MessageType.UserLeft);
        message.RoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task CreateSystemMessage_InvalidType_ThrowsException()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var messageService = CreateMessageService(dataStore);
        var userService = CreateUserService(dataStore);
        var roomService = CreateRoomService(dataStore);

        await userService.RegisterUserAsync("creator");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await messageService.CreateSystemMessageAsync(room!.Id, MessageType.UserMessage, "user1")
        );
    }

    [Fact]
    public async Task Integration_JoinRoom_CreatesSystemMessage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var messageService = CreateMessageService(dataStore);
        var roomService = CreateRoomService(dataStore, messageService);

        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");

        // Act
        await roomService.JoinRoomAsync(room!.Id, "user1");
        var messages = await messageService.GetRoomMessagesAsync(room.Id);

        // Assert
        messages.Should().HaveCount(1);
        var msg = messages.First();
        msg.Type.Should().Be(MessageType.UserJoined);
        msg.Author.Should().Be("SYSTEM");
        msg.Content.Should().Contain("user1");
    }

    [Fact]
    public async Task Integration_LeaveRoom_CreatesSystemMessage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var messageService = CreateMessageService(dataStore);
        var roomService = CreateRoomService(dataStore, messageService);

        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        await roomService.LeaveRoomAsync(room.Id, "user1");
        var messages = await messageService.GetRoomMessagesAsync(room.Id);

        // Assert
        messages.Should().HaveCount(2); // Join + Leave
        var leaveMsg = messages.Last();
        leaveMsg.Type.Should().Be(MessageType.UserLeft);
        leaveMsg.Author.Should().Be("SYSTEM");
        leaveMsg.Content.Should().Contain("user1");
    }

    [Fact]
    public async Task Integration_JoinAndSendMessage_BothMessagesStored()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var userService = CreateUserService(dataStore);
        var messageService = CreateMessageService(dataStore);
        var roomService = CreateRoomService(dataStore, messageService);

        await userService.RegisterUserAsync("creator");
        await userService.RegisterUserAsync("user1");
        var room = await roomService.CreateRoomAsync("TestRoom", "Test", "creator");
        await roomService.JoinRoomAsync(room!.Id, "user1");

        // Act
        await messageService.SendMessageAsync(room.Id, "user1", "Hello everyone!");
        var messages = await messageService.GetRoomMessagesAsync(room.Id);

        // Assert
        messages.Should().HaveCount(2); // System join + user message
        messages.First().Type.Should().Be(MessageType.UserJoined);
        messages.Last().Type.Should().Be(MessageType.UserMessage);
        messages.Last().Content.Should().Be("Hello everyone!");
    }
}
