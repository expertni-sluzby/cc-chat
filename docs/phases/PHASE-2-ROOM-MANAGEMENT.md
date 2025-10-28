# Phase 2: Room Management

## Cíl fáze
Implementovat správu chatovacích místností včetně vytváření, mazání, získání seznamu a vstupu/výstupu z místností.

## Prerekvizity
- Dokončená Phase 1 (User Management)
- Funkční UserService

## Sub-fáze

### 2.1: Vytvoření ChatRoom modelu a DTOs
**Akce:**
- Vytvořit `Models/ChatRoom.cs` - domain model
- Vytvořit `Models/DTOs/CreateRoomRequest.cs`
- Vytvořit `Models/DTOs/RoomResponse.cs`
- Vytvořit `Models/DTOs/RoomDetailResponse.cs`

**ChatRoom model:**
```csharp
public class ChatRoom
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string CreatedBy { get; set; } // nickname
    public DateTime CreatedAt { get; set; }
    public List<string> Participants { get; set; } // nicknames
}
```

**Očekávaný výstup:**
- Kompletní domain model pro ChatRoom
- DTO objekty pro API komunikaci

### 2.2: Rozšíření InMemoryDataStore
**Akce:**
- Přidat `ConcurrentDictionary<Guid, ChatRoom>` pro rooms
- Přidat metody pro CRUD operace s rooms
- Thread-safe operace s participants listem

**Nové metody:**
- `AddRoom(ChatRoom room)`
- `GetRoom(Guid id)`
- `GetAllRooms()`
- `DeleteRoom(Guid id)`
- `AddParticipant(Guid roomId, string nickname)`
- `RemoveParticipant(Guid roomId, string nickname)`

**Očekávaný výstup:**
- Rozšířený storage o room management

### 2.3: Vytvoření RoomService
**Akce:**
- Vytvořit `Services/IRoomService.cs` interface
- Implementovat `Services/RoomService.cs`
- Registrovat v DI jako scoped service

**Metody:**
- `Task<ChatRoom> CreateRoomAsync(string name, string description, string createdBy)`
- `Task<IEnumerable<ChatRoom>> GetAllRoomsAsync()`
- `Task<ChatRoom?> GetRoomByIdAsync(Guid roomId)`
- `Task<bool> DeleteRoomAsync(Guid roomId, string requestingUser)` - only creator
- `Task<bool> JoinRoomAsync(Guid roomId, string nickname)`
- `Task<bool> LeaveRoomAsync(Guid roomId, string nickname)`
- `Task<bool> IsUserInRoomAsync(Guid roomId, string nickname)`

**Business logika:**
- Room name: 3-50 znaků
- Description: max 200 znaků
- Uživatel může být pouze v jedné místnosti najednou
- Smazat místnost může pouze tvůrce
- Join room zkontroluje, že user není již v jiné místnosti

**Očekávaný výstup:**
- Plně funkční RoomService s business logikou

### 2.4: Vytvoření RoomsController
**Akce:**
- Vytvořit `Controllers/RoomsController.cs`
- Implementovat REST endpoints

**Endpoints:**
```
POST   /api/rooms
  Body: { "name": "string", "description": "string", "createdBy": "string" }
  Response: 201 Created + RoomResponse
  Errors: 400 (invalid), 404 (user not found)

GET    /api/rooms
  Response: 200 OK + RoomResponse[]

GET    /api/rooms/{id}
  Response: 200 OK + RoomDetailResponse (including participants)
  Errors: 404 (not found)

DELETE /api/rooms/{id}
  Query: ?requestingUser=nickname
  Response: 204 No Content
  Errors: 403 (not creator), 404 (not found)

POST   /api/rooms/{id}/join
  Body: { "nickname": "string" }
  Response: 200 OK
  Errors: 400 (already in room), 404 (room/user not found)

POST   /api/rooms/{id}/leave
  Body: { "nickname": "string" }
  Response: 200 OK
  Errors: 400 (not in room), 404 (not found)
```

**Očekávaný výstup:**
- Funkční REST API pro room management

### 2.5: Validace a business rules
**Akce:**
- Vytvořit `Validators/CreateRoomRequestValidator.cs`
- Validace room name a description
- Kontrola existence uživatele před akcemi

**Validační pravidla:**
- Name: NotEmpty, Length(3-50)
- Description: MaxLength(200)
- CreatedBy: NotEmpty, musí existovat v systému

**Business rules:**
- User může být jen v 1 místnosti současně
- Při join z jiné místnosti automatický leave

**Očekávaný výstup:**
- Robustní validace a business rules

### 2.6: Audit log pro room events
**Akce:**
- Vytvořit simple logging pro room events
- Log: room created, deleted, user joined, user left
- Využít ILogger

**Očekávaný výstup:**
- Audit trail pro debugging

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Lze vytvořit novou místnost
- [ ] Lze získat seznam všech místností
- [ ] Lze získat detail místnosti včetně účastníků
- [ ] Lze vstoupit do místnosti
- [ ] Lze opustit místnost
- [ ] Uživatel může být jen v jedné místnosti
- [ ] Smazat místnost může jen tvůrce

### Testovací kritéria
- [ ] Unit testy pro RoomService (100% coverage)
- [ ] Integration testy pro RoomsController
- [ ] Validator testy
- [ ] Business rules testy
- [ ] Thread-safety testy

### Dokumentační kritéria
- [ ] API endpoints zdokumentovány v Swagger
- [ ] Business rules zdokumentovány

## Tests

### 2.T1: RoomService - vytvoření místnosti
```csharp
[Fact]
public async Task CreateRoom_ValidData_ReturnsRoom()
{
    // Arrange
    var service = CreateRoomService();
    var userService = CreateUserService();
    await userService.RegisterUserAsync("creator");

    // Act
    var room = await service.CreateRoomAsync("TestRoom", "Description", "creator");

    // Assert
    room.Should().NotBeNull();
    room.Name.Should().Be("TestRoom");
    room.CreatedBy.Should().Be("creator");
    room.Participants.Should().BeEmpty();
}
```

### 2.T2: RoomService - join room
```csharp
[Fact]
public async Task JoinRoom_ValidUser_AddsToParticipants()
{
    // Arrange
    var service = CreateRoomService();
    var room = await service.CreateRoomAsync("Room", "Desc", "creator");

    // Act
    var result = await service.JoinRoomAsync(room.Id, "user1");

    // Assert
    result.Should().BeTrue();
    var updatedRoom = await service.GetRoomByIdAsync(room.Id);
    updatedRoom!.Participants.Should().Contain("user1");
}
```

### 2.T3: RoomService - uživatel může být jen v jedné místnosti
```csharp
[Fact]
public async Task JoinRoom_UserInAnotherRoom_AutomaticallyLeavesOldRoom()
{
    // Arrange
    var service = CreateRoomService();
    var room1 = await service.CreateRoomAsync("Room1", "Desc", "creator");
    var room2 = await service.CreateRoomAsync("Room2", "Desc", "creator");
    await service.JoinRoomAsync(room1.Id, "user1");

    // Act
    await service.JoinRoomAsync(room2.Id, "user1");

    // Assert
    var oldRoom = await service.GetRoomByIdAsync(room1.Id);
    var newRoom = await service.GetRoomByIdAsync(room2.Id);
    oldRoom!.Participants.Should().NotContain("user1");
    newRoom!.Participants.Should().Contain("user1");
}
```

### 2.T4: RoomService - delete room pouze pro tvůrce
```csharp
[Fact]
public async Task DeleteRoom_NotCreator_ReturnsFalse()
{
    // Arrange
    var service = CreateRoomService();
    var room = await service.CreateRoomAsync("Room", "Desc", "creator");

    // Act
    var result = await service.DeleteRoomAsync(room.Id, "otheruser");

    // Assert
    result.Should().BeFalse();
}
```

### 2.T5: RoomsController - integration test
```csharp
[Fact]
public async Task CreateRoom_ValidRequest_Returns201Created()
{
    // Arrange
    var client = CreateTestClient();
    await RegisterUser(client, "creator");
    var request = new
    {
        name = "TestRoom",
        description = "Test",
        createdBy = "creator"
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/rooms", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

### 2.T6: Thread-safety test pro participants
```csharp
[Fact]
public async Task JoinRoom_ConcurrentJoins_AllSucceed()
{
    // Test multiple users joining same room concurrently
    // All should be added to participants list
}
```

## Následující fáze
Po dokončení Phase 2 přejít na **Phase 3: Messaging System**

## Časový odhad
**5-6 hodin** (včetně testování)
