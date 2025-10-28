# SignalR Events Reference

## Connection

**Hub URL:** `/hubs/chat`

## Client → Server Methods

### JoinRoom
Přidá uživatele do místnosti a vrátí historii zpráv.

```typescript
await connection.invoke("JoinRoom", roomId: string, nickname: string)
```

**Parameters:**
- `roomId` - GUID místnosti jako string
- `nickname` - nickname uživatele

**Response:**
- Triggers `RoomHistory` event na caller
- Triggers `UserJoined` event na ostatní v místnosti

**Errors:**
- `HubException` - Invalid room ID format
- `HubException` - User not found. Please register first.
- `HubException` - Room not found
- `HubException` - Failed to join room

---

### LeaveRoom
Odebere uživatele z místnosti.

```typescript
await connection.invoke("LeaveRoom", roomId: string, nickname: string)
```

**Parameters:**
- `roomId` - GUID místnosti jako string
- `nickname` - nickname uživatele

**Response:**
- Triggers `UserLeft` event na ostatní v místnosti

**Errors:**
- `HubException` - Invalid room ID format
- `HubException` - Failed to leave room

---

### SendMessage
Odešle zprávu do místnosti.

```typescript
await connection.invoke("SendMessage", roomId: string, content: string)
```

**Parameters:**
- `roomId` - GUID místnosti jako string
- `content` - obsah zprávy (max 1000 znaků)

**Response:**
- Triggers `ReceiveMessage` event na všechny v místnosti (včetně odesílatele)

**Errors:**
- `HubException` - Connection not authenticated
- `HubException` - Invalid room ID format
- `HubException` - Failed to send message. Check permissions and content.

---

## Server → Client Events

### ReceiveMessage
Nová zpráva byla odeslána do místnosti.

```typescript
connection.on("ReceiveMessage", (message: MessageResponse) => {
  // Handle new message
})
```

**Payload:**
```typescript
interface MessageResponse {
  id: string              // GUID
  roomId: string          // GUID
  author: string          // nickname nebo "SYSTEM"
  content: string         // obsah zprávy (HTML encoded)
  type: MessageType       // 0=UserMessage, 1=UserJoined, 2=UserLeft
  timestamp: string       // ISO 8601 datetime
}
```

**Triggered by:**
- `SendMessage` hub method
- REST API: `POST /api/rooms/{roomId}/messages`

---

### UserJoined
Uživatel vstoupil do místnosti.

```typescript
connection.on("UserJoined", (nickname: string, roomId: string) => {
  // Handle user joined
})
```

**Payload:**
- `nickname` - nickname uživatele který vstoupil
- `roomId` - GUID místnosti jako string

**Triggered by:**
- `JoinRoom` hub method
- REST API: `POST /api/rooms/{roomId}/join`

**Note:** System message je také vytvořena a odeslána přes `ReceiveMessage` event.

---

### UserLeft
Uživatel opustil místnost.

```typescript
connection.on("UserLeft", (nickname: string, roomId: string) => {
  // Handle user left
})
```

**Payload:**
- `nickname` - nickname uživatele který odešel
- `roomId` - GUID místnosti jako string

**Triggered by:**
- `LeaveRoom` hub method
- REST API: `POST /api/rooms/{roomId}/leave`
- Auto-disconnect (pokud byla poslední connection uživatele)

**Note:** System message je také vytvořena a odeslána přes `ReceiveMessage` event.

---

### RoomHistory
Historie zpráv z místnosti (pouze pro volajícího při vstupu).

```typescript
connection.on("RoomHistory", (messages: MessageResponse[]) => {
  // Display message history
})
```

**Payload:**
- Array of `MessageResponse` objektů seřazených chronologicky

**Triggered by:**
- `JoinRoom` hub method - pouze caller dostane tento event

---

### RoomCreated
Nová místnost byla vytvořena.

```typescript
connection.on("RoomCreated", (room: RoomResponse) => {
  // Update room list
})
```

**Payload:**
```typescript
interface RoomResponse {
  id: string                  // GUID
  name: string                // název místnosti
  description: string         // popis místnosti
  createdBy: string          // nickname tvůrce
  createdAt: string          // ISO 8601 datetime
  participantCount: number   // počet účastníků
}
```

**Triggered by:**
- REST API: `POST /api/rooms`

**Audience:** All connected clients

---

### RoomDeleted
Místnost byla smazána.

```typescript
connection.on("RoomDeleted", (roomId: string) => {
  // Remove room from list, leave if in this room
})
```

**Payload:**
- `roomId` - GUID místnosti jako string

**Triggered by:**
- REST API: `DELETE /api/rooms/{roomId}`

**Audience:** All connected clients

---

## Connection Lifecycle

### OnConnected
Automaticky voláno při připojení. Není třeba registrovat handler.

### OnDisconnected
Automaticky voláno při odpojení. Server provede cleanup:
- Odebere connection z ConnectionManager
- Pokud to byla poslední connection uživatele, automaticky ho odhlásí z místnosti
- Triggers `UserLeft` event pokud uživatel byl v místnosti

---

## Error Handling

```typescript
connection.onclose((error) => {
  if (error) {
    console.error("Connection closed with error:", error)
  }
  // Implement reconnection logic
})

connection.onreconnecting((error) => {
  console.log("Reconnecting...", error)
})

connection.onreconnected((connectionId) => {
  console.log("Reconnected with ID:", connectionId)
  // Re-join rooms if needed
})
```

---

## Complete Example

```typescript
import * as signalR from "@microsoft/signalr"

// Create connection
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/chat")
  .withAutomaticReconnect()
  .build()

// Register event handlers
connection.on("ReceiveMessage", (message) => {
  console.log(`${message.author}: ${message.content}`)
  // Update UI with new message
})

connection.on("UserJoined", (nickname, roomId) => {
  console.log(`${nickname} joined room ${roomId}`)
  // Update participant list
})

connection.on("UserLeft", (nickname, roomId) => {
  console.log(`${nickname} left room ${roomId}`)
  // Update participant list
})

connection.on("RoomHistory", (messages) => {
  console.log(`Received ${messages.length} messages`)
  // Display message history
})

connection.on("RoomCreated", (room) => {
  console.log(`New room created: ${room.name}`)
  // Add room to list
})

connection.on("RoomDeleted", (roomId) => {
  console.log(`Room deleted: ${roomId}`)
  // Remove room from list
})

// Start connection
await connection.start()
console.log("Connected to SignalR hub")

// Join a room
try {
  await connection.invoke("JoinRoom", roomId, nickname)
} catch (error) {
  console.error("Failed to join room:", error)
}

// Send a message
try {
  await connection.invoke("SendMessage", roomId, "Hello everyone!")
} catch (error) {
  console.error("Failed to send message:", error)
}

// Leave a room
try {
  await connection.invoke("LeaveRoom", roomId, nickname)
} catch (error) {
  console.error("Failed to leave room:", error)
}
```

---

## Notes

- All GUID values are transmitted as strings
- Messages are HTML encoded on the server for XSS protection
- Multiple connections per user are supported (e.g., multiple browser tabs)
- System messages (UserJoined, UserLeft) are sent as both:
  1. Dedicated events (`UserJoined`, `UserLeft`)
  2. Regular messages via `ReceiveMessage` with `type` = 1 or 2
- Room broadcasts use SignalR Groups for efficient message routing
