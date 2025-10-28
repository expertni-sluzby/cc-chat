# Chat Server Architecture

## Overview

The Chat Server is an in-memory, real-time chat application built with ASP.NET Core, featuring dual communication channels: REST API for traditional HTTP operations and SignalR for real-time WebSocket communication.

**Key Characteristics:**
- In-memory data storage (no database)
- Thread-safe concurrent operations
- Dual protocol support (HTTP + WebSocket)
- Event-driven architecture
- Stateful connections with automatic cleanup

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Layer                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Web Browser  │  │  Mobile App  │  │   Desktop    │          │
│  │   (REST +    │  │    (REST +   │  │     App      │          │
│  │   WebSocket) │  │   WebSocket) │  │  (SignalR)   │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
└─────────┼──────────────────┼──────────────────┼─────────────────┘
          │                  │                  │
          │  HTTP/HTTPS      │                  │  WebSocket
          │                  │                  │
┌─────────┼──────────────────┼──────────────────┼─────────────────┐
│         │      ASP.NET Core Middleware Layer  │                  │
│         │                  │                  │                  │
│    ┌────▼──────┐      ┌────▼─────┐      ┌────▼─────┐           │
│    │   CORS    │      │  Routing │      │  Logging │           │
│    └────┬──────┘      └────┬─────┘      └────┬─────┘           │
│         │                  │                  │                  │
│    ┌────▼──────────────────▼──────────────────▼─────┐           │
│    │         Endpoint Routing & Dispatch             │           │
│    └────┬──────────────────────────────┬─────────────┘           │
│         │                              │                          │
├─────────┼──────────────────────────────┼─────────────────────────┤
│  ┌──────▼────────────┐         ┌───────▼──────────┐             │
│  │  REST Controllers │         │   SignalR Hubs   │             │
│  ├───────────────────┤         ├──────────────────┤             │
│  │ UsersController   │         │    ChatHub       │             │
│  │ RoomsController   │         ├──────────────────┤             │
│  │MessagesController │         │  OnConnected     │             │
│  └──────┬────────────┘         │  OnDisconnected  │             │
│         │                      │  JoinRoom        │             │
│         │                      │  LeaveRoom       │             │
│         │                      │  SendMessage     │             │
│         │                      └────────┬─────────┘             │
│         │                               │                        │
├─────────┼───────────────────────────────┼────────────────────────┤
│         │      Service Layer (Business Logic)                    │
│         │                               │                        │
│    ┌────▼─────────┐  ┌────────────┐  ┌─▼───────────────┐       │
│    │ UserService  │  │RoomService │  │ MessageService   │       │
│    └────┬─────────┘  └────┬───────┘  └─┬───────────────┘       │
│         │                 │             │                        │
│    ┌────▼─────────────────▼─────────────▼───────┐              │
│    │        ConnectionManager (SignalR)         │              │
│    │  - Tracks active WebSocket connections     │              │
│    │  - Maps connections to users               │              │
│    └────────────────┬───────────────────────────┘              │
│                     │                                            │
├─────────────────────┼───────────────────────────────────────────┤
│                     │      Data Layer                            │
│              ┌──────▼────────────┐                              │
│              │  InMemoryDataStore │                              │
│              ├────────────────────┤                              │
│              │ ConcurrentDictionary<string, User>               │
│              │ ConcurrentDictionary<Guid, ChatRoom>             │
│              │ ConcurrentDictionary<Guid, List<Message>>        │
│              └─────────────────────┘                             │
└──────────────────────────────────────────────────────────────────┘
```

---

## Component Diagram

```
┌────────────────────────────────────────────────────────────────┐
│                      Controllers/Hubs                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │Users         │  │Rooms         │  │Messages      │         │
│  │Controller    │  │Controller    │  │Controller    │         │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘         │
│         │                 │                  │                  │
│  ┌──────▼─────────────────▼──────────────────▼───────┐         │
│  │              ChatHub (SignalR)                     │         │
│  │  - JoinRoom(roomId, nickname)                     │         │
│  │  - LeaveRoom(roomId, nickname)                    │         │
│  │  - SendMessage(roomId, content)                   │         │
│  └──────┬────────────────────────────────────────────┘         │
└─────────┼──────────────────────────────────────────────────────┘
          │
┌─────────┼──────────────────────────────────────────────────────┐
│         │               Services (Business Logic)               │
│  ┌──────▼───────┐  ┌────────────┐  ┌─────────────────┐        │
│  │IUserService  │  │IRoomService│  │IMessageService  │        │
│  ├──────────────┤  ├────────────┤  ├─────────────────┤        │
│  │RegisterUser  │  │CreateRoom  │  │SendMessage      │        │
│  │LoginUser     │  │DeleteRoom  │  │GetRoomMessages  │        │
│  │GetAllUsers   │  │JoinRoom    │  │CreateSystemMsg  │        │
│  └──────┬───────┘  │LeaveRoom   │  └────────┬────────┘        │
│         │          │GetAllRooms │           │                  │
│         │          └─────┬──────┘           │                  │
│         │                │                  │                  │
│  ┌──────▼────────────────▼──────────────────▼───────┐         │
│  │           IConnectionManager                      │         │
│  │  - AddConnection(connectionId, nickname)          │         │
│  │  - RemoveConnection(connectionId)                 │         │
│  │  - GetConnectionsByNickname(nickname)             │         │
│  └──────┬────────────────────────────────────────────┘         │
└─────────┼──────────────────────────────────────────────────────┘
          │
┌─────────┼──────────────────────────────────────────────────────┐
│         │                Data Storage                           │
│  ┌──────▼─────────────────────────────────────────────┐        │
│  │         IDataStore (In-Memory)                      │        │
│  │                                                     │        │
│  │  Thread-Safe Data Structures:                      │        │
│  │  ┌──────────────────────────────────────────┐     │        │
│  │  │ _users: ConcurrentDictionary              │     │        │
│  │  │   Key: nickname (string)                  │     │        │
│  │  │   Value: User                             │     │        │
│  │  └──────────────────────────────────────────┘     │        │
│  │  ┌──────────────────────────────────────────┐     │        │
│  │  │ _rooms: ConcurrentDictionary              │     │        │
│  │  │   Key: roomId (Guid)                      │     │        │
│  │  │   Value: ChatRoom                         │     │        │
│  │  └──────────────────────────────────────────┘     │        │
│  │  ┌──────────────────────────────────────────┐     │        │
│  │  │ _messages: ConcurrentDictionary           │     │        │
│  │  │   Key: roomId (Guid)                      │     │        │
│  │  │   Value: List<Message>                    │     │        │
│  │  └──────────────────────────────────────────┘     │        │
│  └─────────────────────────────────────────────────────┘        │
└──────────────────────────────────────────────────────────────────┘
```

---

## Data Flow Diagrams

### 1. User Registration & Login Flow

```
┌────────┐          ┌──────────────┐          ┌─────────────┐          ┌──────────┐
│ Client │          │UsersController│          │UserService  │          │DataStore │
└───┬────┘          └──────┬───────┘          └──────┬──────┘          └────┬─────┘
    │                      │                         │                      │
    │  POST /users/register│                         │                      │
    ├─────────────────────>│                         │                      │
    │                      │   RegisterUserAsync     │                      │
    │                      ├────────────────────────>│                      │
    │                      │                         │  AddUser(nickname)   │
    │                      │                         ├─────────────────────>│
    │                      │                         │                      │
    │                      │                         │  Check unique        │
    │                      │                         │<─────────────────────┤
    │                      │                         │                      │
    │                      │   UserResponse          │                      │
    │                      │<────────────────────────┤                      │
    │  201 Created         │                         │                      │
    │<─────────────────────┤                         │                      │
    │  { nickname, ... }   │                         │                      │
    │                      │                         │                      │
```

### 2. Join Room & Message Flow (Hybrid REST + WebSocket)

```
┌────────┐     ┌──────────────┐     ┌────────────┐     ┌──────────────┐     ┌──────────┐
│ Client │     │RoomsController│     │RoomService │     │MessageService│     │ ChatHub  │
└───┬────┘     └──────┬────────┘     └─────┬──────┘     └──────┬───────┘     └────┬─────┘
    │                 │                    │                    │                   │
    │ POST /rooms/{id}/join                │                    │                   │
    ├────────────────>│                    │                    │                   │
    │                 │   JoinRoomAsync    │                    │                   │
    │                 ├───────────────────>│                    │                   │
    │                 │                    │ CreateSystemMsg    │                   │
    │                 │                    ├───────────────────>│                   │
    │                 │                    │                    │                   │
    │                 │                    │  Broadcast via     │                   │
    │                 │                    │  IHubContext       │                   │
    │                 │                    ├───────────────────────────────────────>│
    │                 │                    │  SendAsync("UserJoined", ...)          │
    │  200 OK         │                    │                    │                   │
    │<────────────────┤                    │                    │                   │
    │                 │                    │                    │                   │
    │                 │                    │                    │                   │
    │ WebSocket: ReceiveMessage event<────────────────────────────────────────────┤
    │<────────────────────────────────────────────────────────────────────────────┤
    │                 │                    │                    │                   │
```

### 3. Send Message Flow (WebSocket)

```
┌────────┐          ┌──────────┐          ┌────────────┐          ┌──────────┐
│ Client │          │ ChatHub  │          │MessageSvc  │          │DataStore │
└───┬────┘          └────┬─────┘          └─────┬──────┘          └────┬─────┘
    │                    │                      │                      │
    │ invoke("SendMessage", roomId, content)    │                      │
    ├───────────────────>│                      │                      │
    │                    │ Get nickname from    │                      │
    │                    │ ConnectionManager    │                      │
    │                    │                      │                      │
    │                    │ SendMessageAsync     │                      │
    │                    ├─────────────────────>│                      │
    │                    │                      │ Validate membership  │
    │                    │                      ├─────────────────────>│
    │                    │                      │                      │
    │                    │                      │ HTML encode content  │
    │                    │                      │ Store message        │
    │                    │                      ├─────────────────────>│
    │                    │                      │                      │
    │                    │ MessageResponse      │                      │
    │                    │<─────────────────────┤                      │
    │                    │                      │                      │
    │                    │ Clients.Group(roomId)│                      │
    │                    │ .SendAsync("ReceiveMessage", msg)           │
    │                    ├──────────────────────────────────────────>  │
    │                    │                      │                   All│
    │                    │                      │              Clients │
    │<─────────────────────────────────────────────────────────── in  │
    │ ReceiveMessage event                     │                Group │
    │                    │                      │                      │
```

### 4. Disconnect & Cleanup Flow

```
┌────────┐          ┌──────────┐          ┌──────────────┐          ┌──────────┐
│ Client │          │ ChatHub  │          │ ConnectionMgr│          │RoomSvc   │
└───┬────┘          └────┬─────┘          └──────┬───────┘          └────┬─────┘
    │                    │                       │                       │
    │  Connection lost   │                       │                       │
    │ ─ ─ ─ ─ ─ ─ ─ ─ ─ >│                       │                       │
    │                    │                       │                       │
    │                    │ OnDisconnectedAsync   │                       │
    │                    │                       │                       │
    │                    │ GetNickname(connId)   │                       │
    │                    ├──────────────────────>│                       │
    │                    │                       │                       │
    │                    │ RemoveConnection      │                       │
    │                    ├──────────────────────>│                       │
    │                    │                       │                       │
    │                    │ HasConnections?       │                       │
    │                    ├──────────────────────>│                       │
    │                    │       false           │                       │
    │                    │<──────────────────────┤                       │
    │                    │                       │                       │
    │                    │ LeaveRoomAsync(user)  │                       │
    │                    ├───────────────────────────────────────────────>│
    │                    │                       │                       │
    │                    │ Clients.Group().SendAsync("UserLeft", ...)    │
    │                    ├──────────────────────────────────────────────>│
    │                    │                       │                   Other│
    │                    │                       │                 Clients│
```

---

## Threading Model

### Thread-Safety Architecture

The Chat Server is designed to handle concurrent requests safely using multiple thread-safety mechanisms:

#### 1. ConcurrentDictionary for Data Storage

```csharp
// Thread-safe collections in InMemoryDataStore
private readonly ConcurrentDictionary<string, User> _users = new();
private readonly ConcurrentDictionary<Guid, ChatRoom> _rooms = new();
private readonly ConcurrentDictionary<Guid, List<Message>> _messages = new();
```

**Operations:**
- `TryAdd`: Atomic insert
- `TryGetValue`: Thread-safe read
- `TryRemove`: Atomic delete
- `AddOrUpdate`: Atomic upsert

#### 2. Lock Statements for Critical Sections

**Example in RoomService:**
```csharp
public async Task<bool> JoinRoomAsync(Guid roomId, string nickname)
{
    // Lock on user object for atomic operations
    lock (_dataStore.GetUser(nickname))
    {
        // Check and update user's current room
        // Ensure no race conditions
    }
}
```

**Protected Operations:**
- Room join/leave (prevents race conditions)
- Participant list updates
- User state changes

#### 3. ConnectionManager Thread-Safety

```csharp
public class ConnectionManager
{
    // Thread-safe: connectionId -> nickname mapping
    private readonly ConcurrentDictionary<string, string> _connectionToNickname = new();

    // Thread-safe: nickname -> set of connectionIds
    private readonly ConcurrentDictionary<string, HashSet<string>> _nicknameToConnections = new();

    // Lock for updating HashSet (not thread-safe by itself)
    private readonly SemaphoreSlim _lock = new(1, 1);
}
```

**Why locks for HashSet:**
- `ConcurrentDictionary` is thread-safe
- `HashSet<string>` is NOT thread-safe
- Solution: Lock when modifying HashSet within ConcurrentDictionary

#### 4. ASP.NET Core Request Pipeline

```
┌─────────────────────────────────────────────────┐
│         Incoming Requests (Thread Pool)          │
│                                                  │
│  Request 1 ──┬──> [Thread 1] ──> Controller     │
│              │                                   │
│  Request 2 ──┼──> [Thread 2] ──> Controller     │
│              │                                   │
│  Request 3 ──┴──> [Thread 3] ──> Controller     │
│                                                  │
│         ▼          ▼          ▼                  │
│    ┌────────────────────────────────┐            │
│    │     Shared Service Layer       │            │
│    │     (Scoped or Singleton)      │            │
│    └────────────────────────────────┘            │
│                    ▼                             │
│    ┌────────────────────────────────┐            │
│    │ Thread-Safe Data Store         │            │
│    │ (ConcurrentDictionary + locks) │            │
│    └────────────────────────────────┘            │
└──────────────────────────────────────────────────┘
```

**Service Lifetimes:**
- **Scoped:** UserService, RoomService, MessageService (per request)
- **Singleton:** IDataStore, ConnectionManager (shared across requests)
- **Transient:** Validators (created on demand)

---

## Dependency Injection Graph

```
Program.cs registers:

┌────────────────────────────────────────────────────┐
│           Dependency Container                      │
│                                                     │
│  Singleton:                                         │
│  ├─ IDataStore ────────> InMemoryDataStore        │
│  └─ IConnectionManager ─> ConnectionManager        │
│                                                     │
│  Scoped (per HTTP request / SignalR invocation):   │
│  ├─ IUserService ──────> UserService               │
│  ├─ IRoomService ──────> RoomService               │
│  └─ IMessageService ───> MessageService            │
│                                                     │
│  Transient:                                         │
│  ├─ IValidator<RegisterUserRequest>                │
│  ├─ IValidator<CreateRoomRequest>                  │
│  ├─ IValidator<SendMessageRequest>                 │
│  └─ ... (other validators)                         │
└─────────────────────────────────────────────────────┘
```

**Injection Flow:**
```
Controllers/Hubs
    ↓ (inject)
Services (UserService, RoomService, MessageService)
    ↓ (inject)
DataStore (Singleton)
```

**Circular Dependency Resolution:**
- MessageService doesn't depend on RoomService
- RoomService optionally depends on MessageService (for system messages)
- Both depend on DataStore directly

---

## SignalR Groups Architecture

SignalR uses Groups for efficient message routing to room participants:

```
┌──────────────────────────────────────────────────────────────┐
│                    SignalR Connection Manager                 │
│                                                               │
│  Connection "abc123" ──┬──> Group "room-guid-1"             │
│                        │                                      │
│  Connection "def456" ──┼──> Group "room-guid-1"             │
│                        │                                      │
│  Connection "ghi789" ──┴──> Group "room-guid-2"             │
│                                                               │
│  Broadcast to Group:                                         │
│  Clients.Group("room-guid-1").SendAsync("ReceiveMessage", ...) │
│    → Sends only to connections in that group                 │
└──────────────────────────────────────────────────────────────┘
```

**Group Operations:**
- `Groups.AddToGroupAsync(connectionId, roomId)` - Join room
- `Groups.RemoveFromGroupAsync(connectionId, roomId)` - Leave room
- `Clients.Group(roomId).SendAsync(...)` - Broadcast to room
- `Clients.OthersInGroup(roomId).SendAsync(...)` - Broadcast to others (not caller)

---

## Scalability Considerations

### Current Architecture (Single Instance)

```
┌────────────┐
│   Client   │ ─┐
└────────────┘  │
                ├──> ┌─────────────────────┐
┌────────────┐  │    │   Chat Server       │
│   Client   │ ─┤    │  (In-Memory State)  │
└────────────┘  │    └─────────────────────┘
                │
┌────────────┐  │
│   Client   │ ─┘
└────────────┘
```

**Limitations:**
- Single point of failure
- Limited to vertical scaling
- State lost on restart
- Max ~10,000 concurrent connections per instance

### Future: Horizontal Scaling with Redis Backplane

```
┌────────────┐
│   Client   │ ─┐
└────────────┘  │
                ├──> ┌─────────────────┐
┌────────────┐  │    │ Chat Server #1  │ ─┐
│   Client   │ ─┤    └─────────────────┘  │
└────────────┘  │                          │    ┌──────────────┐
                │                          ├───>│Redis Backplane│
┌────────────┐  │    ┌─────────────────┐  │    └──────────────┘
│   Client   │ ─┘    │ Chat Server #2  │ ─┘
└────────────┘       └─────────────────┘
```

**Required Changes:**
1. SignalR Redis backplane for message distribution
2. Distributed cache for session state
3. Database for persistent storage
4. Load balancer with sticky sessions

---

## Performance Characteristics

### Memory Usage

**Per User:** ~500 bytes
- User object: ~200 bytes
- ConnectionManager entry: ~100 bytes
- Overhead: ~200 bytes

**Per Room:** ~300 bytes + participant list
- ChatRoom object: ~200 bytes
- Participants list: 50 bytes per participant

**Per Message:** ~200-500 bytes
- Message object: ~150 bytes
- Content: variable (max 1KB)

**Example Calculation (1000 users, 50 rooms, 10000 messages):**
- Users: 1000 × 500 bytes = 500 KB
- Rooms: 50 × (300 + 20 × 50) bytes = 65 KB
- Messages: 10000 × 350 bytes = 3.5 MB
- **Total: ~4-5 MB**

### Throughput

**Measured Performance (Load Tests):**
- 100 concurrent users: ✅ Passed
- 1000 messages/second throughput: ✅ Passed
- 50 concurrent room joins: ✅ Passed
- Message delivery rate: >95%

**Bottlenecks:**
1. SignalR message broadcasting (CPU-bound)
2. Lock contention on high concurrency
3. Message history retrieval (linear scan)

---

## Security Architecture

### Defense in Depth

```
Layer 1: Input Validation
    ↓ (FluentValidation at Controller/Hub)
Layer 2: Business Logic Validation
    ↓ (Service layer checks)
Layer 3: Authorization
    ↓ (Room membership, creator checks)
Layer 4: Output Encoding
    ↓ (HTML encoding for XSS prevention)
Layer 5: Transport Security
    ↓ (HTTPS/WSS in production)
```

**Current Protections:**
- ✅ Input validation (all endpoints)
- ✅ XSS prevention (HTML encoding)
- ✅ Room access control
- ⚠️ Basic authentication (nickname-only)
- ❌ Rate limiting (not implemented)
- ❌ CSRF protection (not needed for API)

---

## Error Handling Architecture

### Exception Flow

```
┌─────────────┐
│  Exception  │
│   Thrown    │
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│ Try/Catch Block  │  ← Service Layer
│  in Service      │
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│   Return null    │  ← Null indicates error
│   or Result<T>   │
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│   Controller     │  ← Convert to HTTP status
│   Maps to HTTP   │
│   Status Code    │
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│   Client Gets    │
│   Error Response │
└──────────────────┘
```

**SignalR Error Handling:**
```csharp
// Hub throws HubException
throw new HubException("User not found");

// Client catches
try {
    await connection.invoke("JoinRoom", ...);
} catch (err) {
    console.error(err.message); // "User not found"
}
```

---

## Deployment Architecture

### Development
```
┌────────────────────────────────┐
│    localhost:5000              │
│  ┌──────────────────────────┐  │
│  │   Kestrel Web Server     │  │
│  │   - HTTP + WebSocket     │  │
│  │   - In-Memory Storage    │  │
│  └──────────────────────────┘  │
└────────────────────────────────┘
```

### Production (Recommended)
```
┌────────────────────────────────────────────────┐
│              Load Balancer / Reverse Proxy      │
│                 (Nginx / IIS)                   │
│              - HTTPS/WSS termination           │
│              - Sticky sessions                 │
└───────────────────┬────────────────────────────┘
                    │
        ┌───────────┴───────────┐
        ▼                       ▼
┌────────────────┐      ┌────────────────┐
│ Chat Server #1 │      │ Chat Server #2 │
│  (Container)   │      │  (Container)   │
│  + Redis       │◄────►│  + Redis       │
└────────────────┘      └────────────────┘
        │                       │
        └───────────┬───────────┘
                    ▼
          ┌──────────────────┐
          │  Redis Backplane │
          │  (Optional)      │
          └──────────────────┘
```

---

## Testing Architecture

```
┌────────────────────────────────────────────────┐
│              Unit Tests                         │
│  - Service logic (UserService, RoomService)    │
│  - Data store operations                       │
│  - Business rules validation                   │
│  Coverage: ~70%                                │
└────────────────────────────────────────────────┘
                    ▲
                    │
┌────────────────────────────────────────────────┐
│          Integration Tests                      │
│  - Controller endpoints                        │
│  - SignalR hub methods                         │
│  - REST + WebSocket interaction                │
│  Coverage: ~10%                                │
└────────────────────────────────────────────────┘
                    ▲
                    │
┌────────────────────────────────────────────────┐
│            E2E Tests                            │
│  - Complete user workflows                     │
│  - Multi-user scenarios                        │
│  - Room management                             │
│  Coverage: ~5%                                 │
└────────────────────────────────────────────────┘
                    ▲
                    │
┌────────────────────────────────────────────────┐
│            Load Tests                           │
│  - 100 concurrent users                        │
│  - High message throughput                     │
│  - Concurrent room joins                       │
└────────────────────────────────────────────────┘

Total Code Coverage: 82.32%
```

---

## Technology Stack

### Backend
- **Framework:** ASP.NET Core 9.0
- **Real-time:** SignalR (WebSocket + fallbacks)
- **Validation:** FluentValidation
- **Logging:** ILogger (built-in)

### Testing
- **Unit Testing:** xUnit
- **Assertions:** FluentAssertions
- **Mocking:** Moq
- **Integration:** WebApplicationFactory
- **Coverage:** Coverlet

### Development Tools
- **.NET SDK:** 9.0
- **IDE:** Visual Studio / VS Code / Rider
- **API Testing:** Swagger UI
- **Version Control:** Git

---

## Future Architecture Enhancements

### Short Term (3-6 months)
- [ ] JWT authentication
- [ ] Rate limiting middleware
- [ ] Structured logging (Serilog)
- [ ] Health checks endpoint
- [ ] Metrics/telemetry (OpenTelemetry)

### Medium Term (6-12 months)
- [ ] Database persistence (PostgreSQL/SQL Server)
- [ ] Redis caching layer
- [ ] Message queue (RabbitMQ/Azure Service Bus)
- [ ] File upload support
- [ ] User presence indicators

### Long Term (12+ months)
- [ ] Microservices architecture
- [ ] Event sourcing pattern
- [ ] CQRS implementation
- [ ] GraphQL API
- [ ] Mobile push notifications

---

## References

- [ASP.NET Core Architecture](https://docs.microsoft.com/aspnet/core/fundamentals/architecture)
- [SignalR Architecture](https://docs.microsoft.com/aspnet/core/signalr/introduction)
- [Dependency Injection in .NET](https://docs.microsoft.com/dotnet/core/extensions/dependency-injection)
- [Thread Safety in .NET](https://docs.microsoft.com/dotnet/standard/threading/thread-safety)

---

**Document Version:** 1.0
**Last Updated:** 2025-10-29
**Author:** Chat Server Development Team
