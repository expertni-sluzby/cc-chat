# Chat Server Project Context

## Project Overview
In-memory chatovací server v .NET s duální podporou REST API a WebSocket komunikace.

## Core Characteristics
- **Database**: None (vše v paměti)
- **Backend**: ASP.NET Core Web API + SignalR
- **Language**: C#
- **Architecture**: In-memory storage, thread-safe operations

## Domain Model

### User
- `Nickname` (string, unique) - primární identifikátor
- `RegisteredAt` (DateTime)
- `IsOnline` (bool)

### ChatRoom
- `Id` (Guid) - unikátní identifikátor
- `Name` (string) - název místnosti
- `Description` (string) - popis místnosti
- `CreatedBy` (string) - nickname tvůrce
- `CreatedAt` (DateTime)
- `Participants` (List<string>) - aktivní účastníci

### Message
- `Id` (Guid)
- `RoomId` (Guid)
- `Author` (string) - nickname (nebo "SYSTEM")
- `Content` (string)
- `Type` (MessageType) - enum: UserMessage, UserJoined, UserLeft
- `Timestamp` (DateTime)

## Functional Requirements

### User Management
- Registrace nového uživatele (nickname)
- Přihlášení existujícího uživatele
- Seznam aktivních uživatelů

### Room Management
- Vytvoření nové místnosti
- Seznam všech místností
- Detail místnosti (včetně účastníků)
- Smazání místnosti (pouze tvůrce)

### Messaging
- Vstup do místnosti (generuje system message)
- Odchod z místnosti (generuje system message)
- Odeslání zprávy do místnosti
- Získání historie zpráv z místnosti

## Technical Architecture

### API Endpoints (REST)
```
POST   /api/users/register
POST   /api/users/login
GET    /api/users

POST   /api/rooms
GET    /api/rooms
GET    /api/rooms/{id}
DELETE /api/rooms/{id}

POST   /api/rooms/{id}/join
POST   /api/rooms/{id}/leave
POST   /api/rooms/{id}/messages
GET    /api/rooms/{id}/messages
```

### WebSocket (SignalR Hubs)
```
Hub: ChatHub

Methods:
- JoinRoom(roomId)
- LeaveRoom(roomId)
- SendMessage(roomId, message)

Events:
- ReceiveMessage(message)
- UserJoined(user, roomId)
- UserLeft(user, roomId)
- RoomCreated(room)
```

### Data Storage
- `ConcurrentDictionary<string, User>` - users by nickname
- `ConcurrentDictionary<Guid, ChatRoom>` - rooms by ID
- `ConcurrentDictionary<Guid, List<Message>>` - messages by roomId

## Implementation Phases

### Phase 0: Project Setup
- ASP.NET Core Web API projekt
- SignalR dependencies
- Project structure
- Base infrastructure

### Phase 1: User Management
- User model
- UserService
- UsersController (REST)
- Unit tests

### Phase 2: Room Management
- ChatRoom model
- RoomService
- RoomsController (REST)
- Unit tests

### Phase 3: Messaging System
- Message model
- MessageService
- MessagesController (REST)
- Integration with rooms
- Unit tests

### Phase 4: WebSocket Integration
- SignalR ChatHub
- Real-time messaging
- Event broadcasting
- Integration tests

### Phase 5: Testing & Documentation
- End-to-end tests
- Integration manual
- API documentation

## Testing Strategy
- Unit tests: xUnit
- Integration tests: WebApplicationFactory
- Coverage: každá fáze má vlastní test suite

## Project Structure
```
cc-chat/
├── src/
│   ├── ChatServer/           # Main API project
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Hubs/
│   │   └── Program.cs
│   └── ChatServer.Tests/     # Test project
├── docs/
│   ├── phases/               # Phase documentation
│   └── integration/          # Integration manual
├── CLAUDE.md                 # This file
└── README.md
```

## Development Guidelines
- Thread-safety: použít ConcurrentDictionary a lock statements
- Error handling: vrátit jasné HTTP status codes
- Validation: FluentValidation pro input validation
- Logging: ILogger pro diagnostiku

## Current Status
- Phase: Planning
- Next Step: Phase 0 - Project Setup
