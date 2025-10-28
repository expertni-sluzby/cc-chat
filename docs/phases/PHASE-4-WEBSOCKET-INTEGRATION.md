# Phase 4: WebSocket Integration (SignalR)

## Cíl fáze
Implementovat real-time komunikaci pomocí SignalR WebSocket pro živé updaty zpráv a room events.

## Prerekvizity
- Dokončená Phase 3 (Messaging System)
- Funkční REST API

## Sub-fáze

### 4.1: Konfigurace SignalR
**Akce:**
- Nakonfigurovat SignalR v Program.cs
- Nastavit CORS pro SignalR
- Nakonfigurovat JSON serialization pro SignalR

**Program.cs změny:**
```csharp
builder.Services.AddSignalR();

app.MapHub<ChatHub>("/hubs/chat");
```

**Očekávaný výstup:**
- SignalR ready pro použití

### 4.2: Vytvoření ChatHub
**Akce:**
- Vytvořit `Hubs/ChatHub.cs`
- Implementovat hub methods

**Hub metody:**
```csharp
public class ChatHub : Hub
{
    // Client -> Server
    public async Task JoinRoom(string roomId, string nickname)
    public async Task LeaveRoom(string roomId, string nickname)
    public async Task SendMessage(string roomId, string message)

    // Server -> Client (defined, called elsewhere)
    // ReceiveMessage(MessageResponse message)
    // UserJoined(string nickname, string roomId)
    // UserLeft(string nickname, string roomId)
    // RoomCreated(RoomResponse room)
    // RoomDeleted(string roomId)
}
```

**Očekávaný výstup:**
- Základní ChatHub s metodami

### 4.3: Connection management
**Akce:**
- Vytvořit `Services/IConnectionManager.cs`
- Implementovat tracking connections to users
- ConcurrentDictionary<connectionId, nickname>
- ConcurrentDictionary<nickname, List<connectionId>> - pro multiple tabs

**Metody:**
- `Task AddConnection(string connectionId, string nickname)`
- `Task RemoveConnection(string connectionId)`
- `Task<string?> GetNicknameByConnectionId(string connectionId)`
- `Task<IEnumerable<string>> GetConnectionsByNickname(string nickname)`

**Očekávaný výstup:**
- Správa WebSocket connections

### 4.4: Implementace ChatHub business logiky
**Akce:**
- Inject dependencies (IUserService, IRoomService, IMessageService, IConnectionManager)
- Implementovat metody s plnou business logikou
- Error handling a validace

**JoinRoom implementace:**
```csharp
public async Task JoinRoom(string roomId, string nickname)
{
    var roomGuid = Guid.Parse(roomId);

    // Validate user exists
    var user = await _userService.GetUserByNicknameAsync(nickname);
    if (user == null)
        throw new HubException("User not found");

    // Join room (creates system message via RoomService)
    var success = await _roomService.JoinRoomAsync(roomGuid, nickname);
    if (!success)
        throw new HubException("Failed to join room");

    // Add to SignalR group
    await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

    // Notify others in room
    await Clients.OthersInGroup(roomId).SendAsync("UserJoined", nickname, roomId);

    // Send history to joining user
    var messages = await _messageService.GetRoomMessagesAsync(roomGuid);
    await Clients.Caller.SendAsync("RoomHistory", messages);
}
```

**Očekávaný výstup:**
- Plně funkční ChatHub s real-time komunikací

### 4.5: Broadcasting events z REST API
**Akce:**
- Inject `IHubContext<ChatHub>` do controllers/services
- Broadcast events při REST API akcích

**Události k broadcastu:**
- Room created -> všem online uživatelům
- Room deleted -> všem online uživatelům
- Message sent via REST -> všem v místnosti

**Implementace v MessagesController:**
```csharp
[HttpPost("{roomId}/messages")]
public async Task<IActionResult> SendMessage(Guid roomId, [FromBody] SendMessageRequest request)
{
    var message = await _messageService.SendMessageAsync(roomId, request.Author, request.Content);

    // Broadcast via SignalR
    await _hubContext.Clients.Group(roomId.ToString())
        .SendAsync("ReceiveMessage", MapToResponse(message));

    return CreatedAtAction(...);
}
```

**Očekávaný výstup:**
- Synchronizace mezi REST a WebSocket

### 4.6: Connection lifecycle events
**Akce:**
- Implementovat `OnConnectedAsync()`
- Implementovat `OnDisconnectedAsync()`
- Auto-cleanup při disconnect

**OnDisconnectedAsync:**
```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    var nickname = await _connectionManager.GetNicknameByConnectionId(Context.ConnectionId);
    if (nickname != null)
    {
        // Remove from connection manager
        await _connectionManager.RemoveConnection(Context.ConnectionId);

        // Check if user has other connections
        var remainingConnections = await _connectionManager.GetConnectionsByNickname(nickname);
        if (!remainingConnections.Any())
        {
            // Last connection - mark user offline
            // Optionally leave rooms
        }
    }

    await base.OnDisconnectedAsync(exception);
}
```

**Očekávaný výstup:**
- Čistý disconnect handling

### 4.7: Error handling a reconnection strategy
**Akce:**
- HubException pro business errors
- Dokumentace reconnection strategie pro klienty
- Keep-alive konfigurace

**Očekávaný výstup:**
- Robustní error handling

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Lze se připojit k ChatHub via WebSocket
- [ ] JoinRoom funguje a vrací historii
- [ ] LeaveRoom funguje a notifikuje ostatní
- [ ] SendMessage funguje real-time
- [ ] REST API broadcastuje přes WebSocket
- [ ] Disconnect cleanup funguje
- [ ] Multiple connections per user podporováno

### Testovací kritéria
- [ ] Integration testy pro ChatHub
- [ ] Mock client testy pro SignalR
- [ ] Connection manager unit testy
- [ ] Broadcast testy

### Dokumentační kritéria
- [ ] SignalR hub dokumentován
- [ ] Client events zdokumentovány
- [ ] Reconnection strategie zdokumentována

## Tests

### 4.T1: ChatHub - join room
```csharp
[Fact]
public async Task JoinRoom_ValidUser_AddsToGroupAndSendsHistory()
{
    // Arrange
    var hub = CreateChatHub();
    await CreateRoomWithMessages("room1");

    // Act
    await hub.JoinRoom("room-guid", "user1");

    // Assert
    // Verify group membership
    // Verify RoomHistory was sent to caller
}
```

### 4.T2: ChatHub - send message
```csharp
[Fact]
public async Task SendMessage_ValidMessage_BroadcastsToGroup()
{
    // Arrange
    var hub = CreateChatHub();
    await JoinUserToRoom(hub, "user1", "room1");

    // Act
    await hub.SendMessage("room1", "Hello!");

    // Assert
    // Verify ReceiveMessage was called on group
}
```

### 4.T3: ConnectionManager
```csharp
[Fact]
public async Task AddConnection_MultipleConnectionsPerUser_TracksAll()
{
    // Arrange
    var manager = new ConnectionManager();

    // Act
    await manager.AddConnection("conn1", "user1");
    await manager.AddConnection("conn2", "user1");

    // Assert
    var connections = await manager.GetConnectionsByNickname("user1");
    connections.Should().HaveCount(2);
}
```

### 4.T4: Disconnect cleanup
```csharp
[Fact]
public async Task OnDisconnected_LastConnection_CleansUpUser()
{
    // Arrange
    var hub = CreateChatHub();
    await hub.JoinRoom("room1", "user1");

    // Act
    await hub.OnDisconnectedAsync(null);

    // Assert
    // Verify connection removed
    // Verify user status updated
}
```

### 4.T5: REST broadcast
```csharp
[Fact]
public async Task SendMessageViaREST_BroadcastsToWebSocket()
{
    // Arrange
    var client = CreateTestClient();
    var hubConnection = CreateHubConnection();
    await hubConnection.StartAsync();

    MessageResponse? receivedMessage = null;
    hubConnection.On<MessageResponse>("ReceiveMessage", msg => receivedMessage = msg);

    // Act
    await client.PostAsJsonAsync("/api/rooms/room1/messages", new { ... });
    await Task.Delay(100); // Wait for broadcast

    // Assert
    receivedMessage.Should().NotBeNull();
}
```

### 4.T6: Error handling
```csharp
[Fact]
public async Task JoinRoom_InvalidRoom_ThrowsHubException()
{
    // Arrange
    var hub = CreateChatHub();

    // Act & Assert
    await Assert.ThrowsAsync<HubException>(() =>
        hub.JoinRoom("invalid-guid", "user1")
    );
}
```

## Následující fáze
Po dokončení Phase 4 přejít na **Phase 5: Testing & Documentation**

## Časový odhad
**6-8 hodin** (včetně testování a integrace)
