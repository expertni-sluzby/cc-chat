# Chat Server - Kompletní implementační plán

## Executive Summary

Tento dokument obsahuje kompletní plán implementace in-memory chatovacího serveru v .NET s duální podporou REST API a WebSocket (SignalR) komunikace.

## Přehled projektu

### Technologie
- **Backend**: ASP.NET Core Web API (.NET 8.0)
- **Real-time**: SignalR
- **Storage**: In-memory (ConcurrentDictionary)
- **Testing**: xUnit, FluentAssertions, Moq
- **Documentation**: Swagger/OpenAPI

### Klíčové vlastnosti
- Database-less (vše v RAM)
- Duální API (REST + WebSocket)
- Thread-safe operace
- Real-time messaging
- Room-based chat
- System notifications

## Fáze implementace

Implementace je rozdělena do 6 fází, každá s vlastním detailním dokumentem:

### Phase 0: Project Setup
**Čas: 2-3 hodiny**

Základní infrastruktura projektu, setup ASP.NET Core Web API, SignalR dependencies, test projekt.

**Deliverables:**
- Funkční Web API aplikace
- Swagger UI
- Test projekt struktura
- In-memory storage infrastruktura

**Detail:** `docs/phases/PHASE-0-PROJECT-SETUP.md`

---

### Phase 1: User Management
**Čas: 4-5 hodin**

Správa uživatelů - registrace, login, seznam uživatelů.

**Deliverables:**
- User model a DTOs
- UserService s business logikou
- UsersController (REST API)
- FluentValidation
- 100% unit test coverage

**Detail:** `docs/phases/PHASE-1-USER-MANAGEMENT.md`

**API Endpoints:**
```
POST /api/users/register
POST /api/users/login
GET  /api/users
```

---

### Phase 2: Room Management
**Čas: 5-6 hodin**

Správa chatovacích místností - CRUD operace, join/leave.

**Deliverables:**
- ChatRoom model a DTOs
- RoomService s business logikou
- RoomsController (REST API)
- Room participant tracking
- 100% unit test coverage

**Detail:** `docs/phases/PHASE-2-ROOM-MANAGEMENT.md`

**API Endpoints:**
```
POST   /api/rooms
GET    /api/rooms
GET    /api/rooms/{id}
DELETE /api/rooms/{id}
POST   /api/rooms/{id}/join
POST   /api/rooms/{id}/leave
```

**Business Rules:**
- Uživatel může být jen v 1 místnosti současně
- Smazat místnost může jen tvůrce

---

### Phase 3: Messaging System
**Čas: 5-6 hodin**

Kompletní messaging včetně historie a systémových zpráv.

**Deliverables:**
- Message model (UserMessage, UserJoined, UserLeft)
- MessageService
- MessagesController (REST API)
- Integrace s RoomService (auto system messages)
- Message history
- 100% unit test coverage

**Detail:** `docs/phases/PHASE-3-MESSAGING-SYSTEM.md`

**API Endpoints:**
```
POST /api/rooms/{roomId}/messages
GET  /api/rooms/{roomId}/messages
```

**Features:**
- Automatické system messages při join/leave
- Historie zpráv seřazená chronologicky
- Validace (pouze účastníci mohou psát)

---

### Phase 4: WebSocket Integration
**Čas: 6-8 hodin**

SignalR implementace pro real-time komunikaci.

**Deliverables:**
- ChatHub (SignalR)
- Connection management
- Real-time broadcasting
- REST + WebSocket synchronizace
- Integration testy

**Detail:** `docs/phases/PHASE-4-WEBSOCKET-INTEGRATION.md`

**Hub Methods (Client -> Server):**
```javascript
JoinRoom(roomId, nickname)
LeaveRoom(roomId, nickname)
SendMessage(roomId, message)
```

**Hub Events (Server -> Client):**
```javascript
ReceiveMessage(message)
UserJoined(nickname, roomId)
UserLeft(nickname, roomId)
RoomCreated(room)
RoomDeleted(roomId)
```

**Features:**
- SignalR groups pro room isolation
- Broadcast events z REST API
- Connection lifecycle management
- Multiple connections per user support

---

### Phase 5: Testing & Documentation
**Čas: 8-10 hodin**

Kompletní testování a dokumentace pro vývojáře.

**Deliverables:**
- End-to-end test scenarios
- Load testing (100 concurrent users)
- Code coverage >80%
- REST API Integration Manual
- WebSocket Integration Manual
- Quickstart Guide
- Architecture Documentation
- Deployment Guide
- Professional README

**Detail:** `docs/phases/PHASE-5-TESTING-DOCUMENTATION.md`

**Dokumentace:**
- `docs/integration/REST-API-MANUAL.md`
- `docs/integration/WEBSOCKET-MANUAL.md`
- `docs/integration/QUICKSTART.md`
- `docs/ARCHITECTURE.md`
- `docs/DEPLOYMENT.md`
- `README.md`

---

## Celkový časový odhad

**30-38 hodin celkem**

| Fáze | Čas |
|------|-----|
| Phase 0: Project Setup | 2-3 h |
| Phase 1: User Management | 4-5 h |
| Phase 2: Room Management | 5-6 h |
| Phase 3: Messaging System | 5-6 h |
| Phase 4: WebSocket Integration | 6-8 h |
| Phase 5: Testing & Documentation | 8-10 h |

## Postup implementace

### Pravidla
1. **Sekvenční implementace** - dodržet pořadí fází
2. **Test-driven** - každá fáze končí testy
3. **Dokumentace průběžně** - komentáře a XML docs
4. **Code review** - kontrola před přechodem na další fázi

### Workflow pro každou fázi
```
1. Přečíst detail fáze (docs/phases/PHASE-X-*.md)
2. Implementovat sub-fáze v pořadí
3. Psát testy průběžně
4. Kontrola "Definition of Done"
5. Commit s popisnou zprávou
6. Přejít na další fázi
```

## Architektura

### Layered Architecture
```
Controllers (REST API)
    ↓
Services (Business Logic)
    ↓
Storage (In-Memory)

Hubs (SignalR)
    ↓
Services (Shared)
    ↓
Storage (Shared)
```

### Data Flow

**REST API Flow:**
```
Client → Controller → Service → Storage → Service → Controller → Client
                                    ↓
                              HubContext → SignalR Clients
```

**WebSocket Flow:**
```
Client → Hub → Service → Storage → Service → Hub → All Clients in Group
```

### Threading Model
- **ConcurrentDictionary** pro thread-safe storage
- **Lock statements** pro complex operations
- **Async/await** pro I/O operations
- **SignalR groups** pro isolation

## Domain Model

```
User
├── Nickname (string, unique)
├── RegisteredAt (DateTime)
├── IsOnline (bool)
└── CurrentRoomId (Guid?)

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
├── Type (MessageType)
└── Timestamp (DateTime)

MessageType (enum)
├── UserMessage
├── UserJoined
└── UserLeft
```

## Quality Assurance

### Testing Strategy
- **Unit Tests**: každý service method
- **Integration Tests**: každý controller endpoint
- **E2E Tests**: kompletní user journeys
- **Load Tests**: 100 concurrent users
- **Coverage Target**: >80%

### Code Quality
- **Validation**: FluentValidation na všech vstupech
- **Error Handling**: konzistentní error responses
- **Logging**: ILogger pro diagnostiku
- **Documentation**: XML comments na public API

## Security Considerations

### Current Implementation
- Nickname-based authentication (simple)
- Room access validation
- Input sanitization
- XSS protection via escaping

### Future Enhancements (out of scope)
- Token-based authentication
- Rate limiting
- Message encryption
- User permissions/roles

## Performance Considerations

### Optimizations
- In-memory storage (fast)
- ConcurrentDictionary (thread-safe, lock-free reads)
- SignalR groups (efficient broadcasting)
- Async operations (non-blocking)

### Limitations
- Memory-only (data lost on restart)
- Single server (no horizontal scaling)
- No persistence layer

### Monitoring
- ILogger for diagnostics
- Performance baselines documented
- Memory usage tracked

## Deployment

### Development
```bash
dotnet run --project src/ChatServer
# Server runs on http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### Production (Docker)
```bash
docker build -t chat-server .
docker run -p 5000:8080 chat-server
```

## Success Criteria

### Funkční
- ✅ Registrace a login uživatelů
- ✅ Vytváření a správa místností
- ✅ Odesílání a příjem zpráv
- ✅ Real-time updates přes WebSocket
- ✅ System notifications (join/leave)
- ✅ Historie zpráv

### Technické
- ✅ REST API kompletní a dokumentované
- ✅ WebSocket API funkční
- ✅ Thread-safe operace
- ✅ >80% code coverage
- ✅ Všechny testy prochází

### Dokumentační
- ✅ Integration manual pro REST
- ✅ Integration manual pro WebSocket
- ✅ Quickstart guide
- ✅ Architecture docs
- ✅ Deployment guide

## Další kroky po dokončení

### Možná rozšíření
1. **Persistence** - SQL Server / PostgreSQL
2. **Authentication** - JWT tokens
3. **Private messaging** - DMs mezi uživateli
4. **File sharing** - upload/download souborů
5. **Reactions** - emoji reactions na zprávy
6. **Search** - fulltextové vyhledávání
7. **Moderation** - admin role, bany
8. **Notifications** - push notifications

### Škálování
1. **Redis** - pro distribuovaný cache
2. **SignalR backplane** - Redis/Azure SignalR Service
3. **Load balancing** - multiple instances
4. **Message queue** - RabbitMQ/Azure Service Bus

## Kontakt a podpora

Pro otázky a problémy během implementace:
- Konzultace s CLAUDE.md pro kontext
- Review phase dokumentů
- Kontrola testů jako reference implementation

---

**Poslední update:** 2025-10-28
**Verze:** 1.0
**Status:** Ready for implementation
