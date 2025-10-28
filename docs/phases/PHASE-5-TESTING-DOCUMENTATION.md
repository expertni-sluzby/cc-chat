# Phase 5: Testing & Documentation

## Cíl fáze
Kompletní end-to-end testování, code coverage analýza a vytvoření finální dokumentace včetně integračního manuálu.

## Prerekvizity
- Dokončená Phase 4 (WebSocket Integration)
- Všechny předchozí unit a integration testy prochází

## Sub-fáze

### 5.1: End-to-End test scenarios
**Akce:**
- Vytvořit `E2ETests/` složku v test projektu
- Implementovat kompletní user journey testy

**Test scenarios:**
1. **Complete Chat Session**
   - Register 2 users
   - Create room
   - Both join via WebSocket
   - Exchange messages
   - One leaves, one stays
   - Verify system messages

2. **Multi-Room scenario**
   - Create multiple rooms
   - User switches between rooms
   - Verify messages only in correct rooms

3. **REST + WebSocket hybrid**
   - Send messages via REST
   - Receive via WebSocket
   - Verify consistency

4. **Concurrent users**
   - 10 users join same room
   - All send messages concurrently
   - Verify all messages delivered

**Očekávaný výstup:**
- Kompletní E2E test suite

### 5.2: Performance a load testing
**Akce:**
- Vytvořit basic load test
- Test s 100 concurrent connections
- Měření: response time, memory usage

**Test areas:**
- Concurrent room joins
- High message throughput
- Memory leaks při long-running sessions

**Očekávaný výstup:**
- Performance baseline dokumentace

### 5.3: Code coverage analýza
**Akce:**
- Nainstalovat coverlet.collector
- Generovat code coverage report
- Cíl: >80% coverage

**Příkazy:**
```bash
dotnet add package coverlet.collector
dotnet test --collect:"XPlat Code Coverage"
```

**Očekávaný výstup:**
- Coverage report v HTML formátu
- Identifikace untested code paths

### 5.4: Security audit
**Akce:**
- Review input validation
- XSS protection check
- Rate limiting considerations
- Authentication concerns (pro budoucí rozšíření)

**Checklist:**
- [ ] Všechny vstupy validovány
- [ ] Zprávy escaped/sanitized
- [ ] Room access kontrolován
- [ ] WebSocket autentizace (basic via nickname)

**Očekávaný výstup:**
- Security checklist dokument

### 5.5: Integration manual - REST API
**Akce:**
- Vytvořit kompletní REST API dokumentaci
- Příklady requestů/responses
- Error codes
- cURL příklady

**Struktura:**
```markdown
# REST API Integration Manual

## Authentication
Currently nickname-based, no tokens required.

## Endpoints

### Users
POST /api/users/register
GET /api/users
...

### Rooms
POST /api/rooms
...

### Messages
POST /api/rooms/{id}/messages
...

## Error Handling
## Rate Limiting
## Best Practices
```

**Očekávaný výstup:**
- `docs/integration/REST-API-MANUAL.md`

### 5.6: Integration manual - WebSocket
**Akce:**
- Vytvořit SignalR client guide
- JavaScript příklad
- C# příklad
- Python příklad (volitelně)

**Obsahuje:**
- Connection setup
- Hub method calls
- Event handling
- Reconnection strategie
- Error handling

**JavaScript příklad:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/chat")
    .build();

connection.on("ReceiveMessage", (message) => {
    console.log("New message:", message);
});

await connection.start();
await connection.invoke("JoinRoom", roomId, nickname);
```

**Očekávaný výstup:**
- `docs/integration/WEBSOCKET-MANUAL.md`

### 5.7: Complete API reference
**Akce:**
- Vytvořit OpenAPI/Swagger export
- Generovat API documentation
- Postman collection (optional)

**Očekávaný výstup:**
- `docs/integration/openapi.json`
- Swagger UI dostupné na `/swagger`

### 5.8: Developer quickstart guide
**Akce:**
- Vytvořit quick start pro nové vývojáře
- Step-by-step tutorial
- Sample client code

**Obsahuje:**
```markdown
# Quickstart Guide

## 1. Prerequisites
## 2. Running the Server
## 3. Your First Chat Client (10 minutes)
   - Register user
   - Create room
   - Send message via REST
## 4. Adding Real-time Updates (10 minutes)
   - Connect to WebSocket
   - Listen for messages
## 5. Next Steps
```

**Očekávaný výstup:**
- `docs/integration/QUICKSTART.md`

### 5.9: Architecture documentation
**Akce:**
- Diagram architektury
- Data flow diagrams
- Threading model explanation

**Očekávaný výstup:**
- `docs/ARCHITECTURE.md`

### 5.10: Deployment guide
**Akce:**
- Docker support
- Docker-compose pro lokální dev
- Production considerations

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "ChatServer.dll"]
```

**Očekávaný výstup:**
- `Dockerfile`
- `docker-compose.yml`
- `docs/DEPLOYMENT.md`

### 5.11: Final README
**Akce:**
- Kompletní README.md
- Features overview
- Quick links k dokumentaci

**Očekávaný výstup:**
- Professional README.md

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Všechny E2E testy prochází
- [ ] Code coverage >80%
- [ ] Performance baseline zdokumentována

### Testovací kritéria
- [ ] E2E test suite kompletní
- [ ] Load test existuje
- [ ] Všechny testy prochází

### Dokumentační kritéria
- [ ] REST API manual kompletní
- [ ] WebSocket manual kompletní
- [ ] Quickstart guide hotový
- [ ] README.md profesionální
- [ ] Architecture dokumentace hotová
- [ ] Deployment guide hotový

## Tests

### 5.T1: E2E - Complete chat session
```csharp
[Fact]
public async Task E2E_CompleteChatSession_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    var hub1 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/chat")
        .Build();
    var hub2 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hubs/chat")
        .Build();

    List<MessageResponse> user1Messages = new();
    List<MessageResponse> user2Messages = new();

    hub1.On<MessageResponse>("ReceiveMessage", msg => user1Messages.Add(msg));
    hub2.On<MessageResponse>("ReceiveMessage", msg => user2Messages.Add(msg));

    // Act
    // 1. Register users
    await client.PostAsJsonAsync("/api/users/register", new { nickname = "alice" });
    await client.PostAsJsonAsync("/api/users/register", new { nickname = "bob" });

    // 2. Create room
    var roomResponse = await client.PostAsJsonAsync("/api/rooms", new
    {
        name = "Test Room",
        description = "E2E Test",
        createdBy = "alice"
    });
    var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

    // 3. Both join via WebSocket
    await hub1.StartAsync();
    await hub2.StartAsync();
    await hub1.InvokeAsync("JoinRoom", room!.Id.ToString(), "alice");
    await hub2.InvokeAsync("JoinRoom", room.Id.ToString(), "bob");

    // 4. Exchange messages
    await hub1.InvokeAsync("SendMessage", room.Id.ToString(), "Hello Bob!");
    await hub2.InvokeAsync("SendMessage", room.Id.ToString(), "Hi Alice!");

    await Task.Delay(500); // Wait for broadcasts

    // Assert
    user1Messages.Should().HaveCountGreaterThan(0);
    user2Messages.Should().HaveCountGreaterThan(0);
    user1Messages.Should().Contain(m => m.Content == "Hi Alice!");
    user2Messages.Should().Contain(m => m.Content == "Hello Bob!");
}
```

### 5.T2: Load test - 100 concurrent users
```csharp
[Fact]
public async Task LoadTest_100ConcurrentUsers_AllMessagesDelivered()
{
    // Create 100 hub connections
    // All join same room
    // Each sends 10 messages
    // Verify all 1000 messages delivered to all users
}
```

### 5.T3: Memory leak test
```csharp
[Fact]
public async Task MemoryTest_LongRunningSession_NoLeaks()
{
    // Monitor memory over 1000 message sends
    // Verify memory doesn't grow unbounded
}
```

## Následující krok
**Projekt je kompletní a připravený k nasazení!**

## Časový odhad
**8-10 hodin** (včetně dokumentace)
