# Phase 3: Messaging System

## Cíl fáze
Implementovat kompletní messaging systém včetně odesílání zpráv, historie a systémových zpráv o vstupu/výstupu uživatelů.

## Prerekvizity
- Dokončená Phase 2 (Room Management)
- Funkční RoomService a UserService

## Sub-fáze

### 3.1: Vytvoření Message modelu a DTOs
**Akce:**
- Vytvořit `Models/Message.cs` - domain model
- Vytvořit `Models/MessageType.cs` - enum
- Vytvořit `Models/DTOs/SendMessageRequest.cs`
- Vytvořit `Models/DTOs/MessageResponse.cs`

**MessageType enum:**
```csharp
public enum MessageType
{
    UserMessage,
    UserJoined,
    UserLeft
}
```

**Message model:**
```csharp
public class Message
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string Author { get; set; } // nickname or "SYSTEM"
    public string Content { get; set; }
    public MessageType Type { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Očekávaný výstup:**
- Kompletní domain model pro Message
- DTO objekty pro API komunikaci

### 3.2: Rozšíření InMemoryDataStore
**Akce:**
- Přidat `ConcurrentDictionary<Guid, ConcurrentBag<Message>>` pro messages by roomId
- Thread-safe operace s messages

**Nové metody:**
- `AddMessage(Message message)`
- `GetMessagesByRoomId(Guid roomId)`
- `GetRoomMessageCount(Guid roomId)`
- `ClearRoomMessages(Guid roomId)` - pro delete room

**Očekávaný výstup:**
- Storage podporuje zprávy s historií

### 3.3: Vytvoření MessageService
**Akce:**
- Vytvořit `Services/IMessageService.cs` interface
- Implementovat `Services/MessageService.cs`
- Registrovat v DI jako scoped service

**Metody:**
- `Task<Message> SendMessageAsync(Guid roomId, string author, string content)`
- `Task<IEnumerable<Message>> GetRoomMessagesAsync(Guid roomId, int? limit = null)`
- `Task<Message> CreateSystemMessageAsync(Guid roomId, MessageType type, string username)`

**Business logika:**
- Content: max 1000 znaků
- User musí být v místnosti pro odeslání zprávy
- System messages: auto-generované texty
  - UserJoined: "{username} vstoupil do místnosti"
  - UserLeft: "{username} opustil místnost"
- Messages seřazeny podle Timestamp

**Očekávaný výstup:**
- Plně funkční MessageService

### 3.4: Integrace se RoomService
**Akce:**
- Upravit `RoomService.JoinRoomAsync()` - vytvoří system message
- Upravit `RoomService.LeaveRoomAsync()` - vytvoří system message
- Dependency injection MessageService do RoomService

**Změny:**
```csharp
// JoinRoomAsync
public async Task<bool> JoinRoomAsync(Guid roomId, string nickname)
{
    // ... existing logic ...
    await _messageService.CreateSystemMessageAsync(roomId, MessageType.UserJoined, nickname);
    return true;
}

// LeaveRoomAsync
public async Task<bool> LeaveRoomAsync(Guid roomId, string nickname)
{
    // ... existing logic ...
    await _messageService.CreateSystemMessageAsync(roomId, MessageType.UserLeft, nickname);
    return true;
}
```

**Očekávaný výstup:**
- Automatické system messages při join/leave

### 3.5: Vytvoření MessagesController
**Akce:**
- Vytvořit `Controllers/MessagesController.cs`
- Implementovat REST endpoints

**Endpoints:**
```
POST   /api/rooms/{roomId}/messages
  Body: { "author": "nickname", "content": "string" }
  Response: 201 Created + MessageResponse
  Errors: 400 (invalid), 403 (not in room), 404 (room not found)

GET    /api/rooms/{roomId}/messages
  Query: ?limit=100
  Response: 200 OK + MessageResponse[]
  Errors: 404 (room not found)
```

**Očekávaný výstup:**
- Funkční REST API pro messaging

### 3.6: Validace a security
**Akce:**
- Vytvořit `Validators/SendMessageRequestValidator.cs`
- Kontrola, že uživatel je v místnosti před odesláním
- Rate limiting considerations (dokumentace, ne implementace)

**Validační pravidla:**
- Content: NotEmpty, MaxLength(1000)
- Author: NotEmpty, musí existovat
- Author musí být participant místnosti

**Očekávaný výstup:**
- Validace a security checks

### 3.7: Message formatting a sanitization
**Akce:**
- Trim whitespace z content
- Basic HTML escaping (pokud bude frontend zobrazovat)
- Dokumentace o tom, že frontend má zodpovědnost za XSS protection

**Očekávaný výstup:**
- Safe message handling

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Lze odeslat zprávu do místnosti
- [ ] Pouze účastníci místnosti mohou posílat zprávy
- [ ] Lze získat historii zpráv z místnosti
- [ ] Join/Leave automaticky vytváří system messages
- [ ] Zprávy seřazeny chronologicky
- [ ] Limit na délku zprávy funguje

### Testovací kritéria
- [ ] Unit testy pro MessageService (100% coverage)
- [ ] Integration testy pro MessagesController
- [ ] Testy integrace s RoomService
- [ ] Validator testy
- [ ] Thread-safety testy

### Dokumentační kritéria
- [ ] API endpoints zdokumentovány v Swagger
- [ ] Message format zdokumentován
- [ ] Security considerations zdokumentovány

## Tests

### 3.T1: MessageService - odeslání zprávy
```csharp
[Fact]
public async Task SendMessage_ValidData_ReturnsMessage()
{
    // Arrange
    var service = CreateMessageService();
    var room = await CreateRoomWithUser("user1");

    // Act
    var message = await service.SendMessageAsync(room.Id, "user1", "Hello!");

    // Assert
    message.Should().NotBeNull();
    message.Content.Should().Be("Hello!");
    message.Author.Should().Be("user1");
    message.Type.Should().Be(MessageType.UserMessage);
}
```

### 3.T2: MessageService - system message
```csharp
[Fact]
public async Task CreateSystemMessage_UserJoined_CreatesCorrectMessage()
{
    // Arrange
    var service = CreateMessageService();
    var room = await CreateRoom();

    // Act
    var message = await service.CreateSystemMessageAsync(
        room.Id,
        MessageType.UserJoined,
        "user1"
    );

    // Assert
    message.Author.Should().Be("SYSTEM");
    message.Content.Should().Contain("user1");
    message.Content.Should().Contain("vstoupil");
    message.Type.Should().Be(MessageType.UserJoined);
}
```

### 3.T3: MessageService - historie
```csharp
[Fact]
public async Task GetRoomMessages_MultipleMessages_ReturnsInChronologicalOrder()
{
    // Arrange
    var service = CreateMessageService();
    var room = await CreateRoomWithUser("user1");
    await service.SendMessageAsync(room.Id, "user1", "First");
    await Task.Delay(10);
    await service.SendMessageAsync(room.Id, "user1", "Second");

    // Act
    var messages = await service.GetRoomMessagesAsync(room.Id);

    // Assert
    var list = messages.ToList();
    list[0].Content.Should().Be("First");
    list[1].Content.Should().Be("Second");
}
```

### 3.T4: Integration - join vytváří system message
```csharp
[Fact]
public async Task JoinRoom_CreatesSystemMessage()
{
    // Arrange
    var roomService = CreateRoomService();
    var messageService = CreateMessageService();
    var room = await roomService.CreateRoomAsync("Room", "Desc", "creator");

    // Act
    await roomService.JoinRoomAsync(room.Id, "user1");
    var messages = await messageService.GetRoomMessagesAsync(room.Id);

    // Assert
    messages.Should().HaveCount(1);
    messages.First().Type.Should().Be(MessageType.UserJoined);
}
```

### 3.T5: MessagesController - integration test
```csharp
[Fact]
public async Task SendMessage_ValidRequest_Returns201Created()
{
    // Arrange
    var client = CreateTestClient();
    var room = await CreateRoomAndJoinUser(client, "user1");
    var request = new { author = "user1", content = "Hello!" };

    // Act
    var response = await client.PostAsJsonAsync(
        $"/api/rooms/{room.Id}/messages",
        request
    );

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

### 3.T6: Security - nelze poslat zprávu pokud nejste v místnosti
```csharp
[Fact]
public async Task SendMessage_UserNotInRoom_Returns403Forbidden()
{
    // Arrange
    var client = CreateTestClient();
    var room = await CreateRoom(client, "creator");
    var request = new { author = "outsider", content = "Hello!" };

    // Act
    var response = await client.PostAsJsonAsync(
        $"/api/rooms/{room.Id}/messages",
        request
    );

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### 3.T7: Thread-safety test
```csharp
[Fact]
public async Task SendMessage_ConcurrentSends_AllMessagesStored()
{
    // Test multiple users sending messages concurrently to same room
    // All messages should be stored
}
```

## Následující fáze
Po dokončení Phase 3 přejít na **Phase 4: WebSocket Integration**

## Časový odhad
**5-6 hodin** (včetně testování a integrace)
