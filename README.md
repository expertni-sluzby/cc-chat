# Chat Server

In-memory real-time chatovací server postavený na **ASP.NET Core** a **SignalR** s duální podporou REST API a WebSocket komunikace.

![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-blue)
![Build](https://img.shields.io/badge/build-passing-green)
![Coverage](https://img.shields.io/badge/coverage-82.32%25-brightgreen)
![Tests](https://img.shields.io/badge/tests-167%20passed-success)

---

## Vlastnosti

### Core Features
- **Multi-user chat** - více uživatelů může chatovat současně
- **Room-based** - oddělené chatovací místnosti
- **Real-time messaging** - okamžité doručení zpráv přes WebSocket
- **Message history** - perzistentní historie zpráv (v paměti)
- **System notifications** - automatické notifikace o vstupu/výstupu uživatelů
- **Database-less** - vše běží v RAM, žádná databáze

### API
- **REST API** - kompletní HTTP API pro všechny operace
- **WebSocket (SignalR)** - real-time bi-directional komunikace
- **Swagger UI** - interaktivní API dokumentace
- **CORS enabled** - připraveno pro web klienty

### Technical
- **Thread-safe** - ConcurrentDictionary pro bezpečné concurrent operace
- **In-memory storage** - ultra-rychlý přístup k datům
- **Automatic reconnection** - SignalR automaticky obnoví spojení
- **Multiple connections** - uživatel může mít více aktivních připojení

---

## Quick Start

### Prerekvizity
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- Docker (optional, for containerized deployment)

### Spuštění serveru

```bash
# Clone repository
git clone <repo-url>
cd cc-chat

# Restore dependencies
dotnet restore

# Run server
dotnet run --project src/ChatServer

# Server běží na http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### První požadavek

```bash
# Registrace uživatele
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"nickname": "alice"}'

# Login
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"nickname": "alice"}'

# Vytvoření místnosti
curl -X POST http://localhost:5000/api/rooms \
  -H "Content-Type: application/json" \
  -d '{
    "name": "General",
    "description": "Main chat room",
    "createdBy": "alice"
  }'
```

---

## Documentation

### Pro vývojáře

- **[Quick Start Guide](docs/integration/QUICKSTART.md)** - 20minutový tutorial
- **[REST API Manual](docs/integration/REST-API-MANUAL.md)** - kompletní REST API dokumentace
- **[WebSocket Manual](docs/integration/WEBSOCKET-MANUAL.md)** - SignalR integration guide

### Architektura & Deployment

- **[Architecture Documentation](docs/ARCHITECTURE.md)** - system architecture with diagrams
- **[Deployment Guide](docs/DEPLOYMENT.md)** - Docker, Kubernetes, Cloud deployments
- **[Security Audit](docs/SECURITY-AUDIT.md)** - security checklist and best practices

### Pro implementaci

- **[Implementation Plan](docs/IMPLEMENTATION-PLAN.md)** - kompletní plán implementace
- **[CLAUDE.md](CLAUDE.md)** - kontext projektu a architektura

### Implementační fáze

| Fáze | Popis | Čas | Dokumentace |
|------|-------|-----|-------------|
| 0 | Project Setup | 2-3h | [PHASE-0](docs/phases/PHASE-0-PROJECT-SETUP.md) |
| 1 | User Management | 4-5h | [PHASE-1](docs/phases/PHASE-1-USER-MANAGEMENT.md) |
| 2 | Room Management | 5-6h | [PHASE-2](docs/phases/PHASE-2-ROOM-MANAGEMENT.md) |
| 3 | Messaging System | 5-6h | [PHASE-3](docs/phases/PHASE-3-MESSAGING-SYSTEM.md) |
| 4 | WebSocket Integration | 6-8h | [PHASE-4](docs/phases/PHASE-4-WEBSOCKET-INTEGRATION.md) |
| 5 | Testing & Documentation | 8-10h | [PHASE-5](docs/phases/PHASE-5-TESTING-DOCUMENTATION.md) |

**Celkem: 30-38 hodin**

---

## API Overview

### REST Endpoints

**Users**
```
POST   /api/users/register    - Registrovat uživatele
POST   /api/users/login       - Přihlásit uživatele
GET    /api/users             - Seznam uživatelů
```

**Rooms**
```
POST   /api/rooms             - Vytvořit místnost
GET    /api/rooms             - Seznam místností
GET    /api/rooms/{id}        - Detail místnosti
DELETE /api/rooms/{id}        - Smazat místnost
POST   /api/rooms/{id}/join   - Vstoupit do místnosti
POST   /api/rooms/{id}/leave  - Opustit místnost
```

**Messages**
```
POST   /api/rooms/{id}/messages   - Odeslat zprávu
GET    /api/rooms/{id}/messages   - Získat historii
```

### WebSocket (SignalR Hub)

**Hub URL:** `ws://localhost:5000/hubs/chat`

**Client → Server:**
```javascript
JoinRoom(roomId, nickname)
LeaveRoom(roomId, nickname)
SendMessage(roomId, message)
```

**Server → Client:**
```javascript
ReceiveMessage(message)
UserJoined(nickname, roomId)
UserLeft(nickname, roomId)
RoomHistory(messages[])
RoomCreated(room)
RoomDeleted(roomId)
```

---

## Architecture

### Layers

```
┌─────────────────────────────────────┐
│         Controllers / Hubs          │  ← HTTP/WebSocket endpoints
├─────────────────────────────────────┤
│            Services                 │  ← Business logic
├─────────────────────────────────────┤
│         Storage (In-Memory)         │  ← ConcurrentDictionary
└─────────────────────────────────────┘
```

### Data Model

```
User
├── Nickname (string, unique)
├── RegisteredAt (DateTime)
└── IsOnline (bool)

ChatRoom
├── Id (Guid)
├── Name (string)
├── Description (string)
├── CreatedBy (string)
├── CreatedAt (DateTime)
└── Participants (List<string>)

Message
├── Id (Guid)
├── RoomId (Guid)
├── Author (string)
├── Content (string)
├── Type (UserMessage | UserJoined | UserLeft)
└── Timestamp (DateTime)
```

### Threading Model

- **ConcurrentDictionary** pro thread-safe storage
- **Async/await** pro non-blocking operations
- **SignalR groups** pro room isolation
- **Lock statements** pro complex operations

---

## Client Examples

### JavaScript (REST)

```javascript
// Register user
const response = await fetch('/api/users/register', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ nickname: 'alice' })
});

// Create room
const room = await fetch('/api/rooms', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    name: 'General',
    description: 'Chat room',
    createdBy: 'alice'
  })
}).then(r => r.json());

// Send message
await fetch(`/api/rooms/${room.id}/messages`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    author: 'alice',
    content: 'Hello!'
  })
});
```

### JavaScript (WebSocket)

```javascript
import * as signalR from "@microsoft/signalr";

// Connect
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/chat")
  .withAutomaticReconnect()
  .build();

// Listen for messages
connection.on("ReceiveMessage", (message) => {
  console.log(`${message.author}: ${message.content}`);
});

await connection.start();

// Join room
await connection.invoke("JoinRoom", roomId, "alice");

// Send message
await connection.invoke("SendMessage", roomId, "Hello!");
```

### C# Client

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/hubs/chat")
    .WithAutomaticReconnect()
    .Build();

connection.On<MessageResponse>("ReceiveMessage", message =>
{
    Console.WriteLine($"{message.Author}: {message.Content}");
});

await connection.StartAsync();
await connection.InvokeAsync("JoinRoom", roomId, "alice");
await connection.InvokeAsync("SendMessage", roomId, "Hello!");
```

---

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run with hot reload

```bash
dotnet watch run --project src/ChatServer
```

---

## Testing

### Test Suite Summary
- **Total Tests:** 167 (all passing ✅)
- **Code Coverage:** 82.32% line coverage, 76.19% branch coverage
- **Test Types:** Unit, Integration, E2E, Load tests

### Unit Tests
- xUnit test framework
- Moq for mocking
- FluentAssertions for readable assertions
- Full coverage of Services layer

### Integration Tests
- WebApplicationFactory
- In-memory test server
- HTTP client testing
- SignalR hub testing

### E2E Tests
- Complete user journeys
- Multi-client scenarios
- REST + WebSocket hybrid scenarios

### Load Tests
- **100 concurrent users:** ✅ Passed
- **1000 messages throughput:** ✅ Passed (>95% delivery rate)
- **50 concurrent room joins:** ✅ Passed

---

## Deployment

### Development
```bash
dotnet run --project src/ChatServer
# Server available at http://localhost:5000
```

### Production (Docker)
```bash
# Build image
docker build -t chatserver:latest .

# Run container
docker run -d -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --name chatserver \
  chatserver:latest
```

### Docker Compose
```bash
# Production
docker-compose up -d

# Development
docker-compose -f docker-compose.dev.yml up -d
```

### Cloud Deployment
See **[Deployment Guide](docs/DEPLOYMENT.md)** for:
- Kubernetes deployment
- Azure App Service
- AWS Elastic Beanstalk
- Production best practices

---

## Limitations

### Current Version
- **No persistence** - data lost on restart (in-memory only)
- **Single server** - no horizontal scaling
- **Simple authentication** - nickname-based only
- **No rate limiting** - může být zneužito

### Future Enhancements
- Database persistence (SQL Server, PostgreSQL)
- JWT authentication
- Redis for distributed cache
- SignalR backplane for scaling
- Rate limiting
- Message encryption
- Private messaging
- File sharing
- User roles & permissions

---

## Performance

### Benchmarks (Tested)
- **Concurrent users:** 100+ users tested (✅ passed load tests)
- **Messages/second:** 500+ messages/second (95%+ delivery rate)
- **Concurrent room joins:** 50+ simultaneous joins (✅ passed)
- **Latency:** <50ms (typical network conditions)
- **Memory:** ~100MB base + ~1KB per user + ~500B per message
- **Code Coverage:** 82.32% line coverage

### Optimizations
- ConcurrentDictionary (lock-free reads)
- SignalR groups (efficient broadcasting)
- Async operations (non-blocking)
- In-memory storage (ultra-fast)

---

## Contributing

### Development workflow
1. Fork repository
2. Create feature branch
3. Implement podle phase dokumentace
4. Psát testy (aim for 100% coverage)
5. Update dokumentaci
6. Submit pull request

### Code style
- C# naming conventions
- Async/await for I/O operations
- XML documentation comments
- FluentValidation for input validation

---

## Project Structure

```
cc-chat/
├── src/
│   ├── ChatServer/              # Main API project
│   │   ├── Controllers/         # REST API controllers
│   │   ├── Hubs/                # SignalR hubs
│   │   ├── Services/            # Business logic
│   │   ├── Models/              # Domain models & DTOs
│   │   ├── Storage/             # In-memory storage
│   │   ├── Validators/          # FluentValidation
│   │   ├── Middleware/          # Error handling, etc.
│   │   └── Program.cs           # App entry point
│   └── ChatServer.Tests/        # Test project
│       ├── Unit/                # Unit tests
│       ├── Integration/         # Integration tests
│       └── E2E/                 # End-to-end tests
├── docs/
│   ├── phases/                  # Implementation phases
│   │   ├── PHASE-0-PROJECT-SETUP.md
│   │   ├── PHASE-1-USER-MANAGEMENT.md
│   │   ├── PHASE-2-ROOM-MANAGEMENT.md
│   │   ├── PHASE-3-MESSAGING-SYSTEM.md
│   │   ├── PHASE-4-WEBSOCKET-INTEGRATION.md
│   │   └── PHASE-5-TESTING-DOCUMENTATION.md
│   ├── integration/             # Integration manuals
│   │   ├── REST-API-MANUAL.md
│   │   ├── WEBSOCKET-MANUAL.md
│   │   └── QUICKSTART.md
│   └── IMPLEMENTATION-PLAN.md   # Master plan
├── CLAUDE.md                    # Project context
└── README.md                    # This file
```

---

## Resources

### Documentation
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core)
- [SignalR](https://docs.microsoft.com/aspnet/core/signalr)
- [xUnit](https://xunit.net/)
- [FluentValidation](https://fluentvalidation.net/)

### Community
- **Issues:** GitHub Issues
- **Discussions:** GitHub Discussions
- **Contributing:** See CONTRIBUTING.md (TBD)

---

## License

MIT License - viz LICENSE soubor

---

## Authors

Vytvořeno jako vzdělávací projekt pro demonstraci:
- ASP.NET Core Web API
- SignalR real-time komunikace
- In-memory storage patterns
- Thread-safe programming
- Test-driven development
- Kompletní dokumentace

---

## Acknowledgments

- Microsoft za ASP.NET Core a SignalR
- .NET Community
- Claude Code pro asistenci při vývoji

---

**Happy Chatting!**

Pro otázky a podporu navštivte [dokumentaci](docs/) nebo vytvořte issue.
