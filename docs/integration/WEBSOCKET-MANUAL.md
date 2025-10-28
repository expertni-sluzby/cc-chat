# Chat Server - WebSocket Integration Manual (SignalR)

**Version:** 1.0
**Last Updated:** 2025-10-28

## Přehled

Chat Server podporuje real-time komunikaci pomocí SignalR (WebSocket). SignalR poskytuje automatický fallback na jiné transportní protokoly pokud WebSocket není dostupný.

### Hub URL
```
ws://localhost:5000/hubs/chat
```

### Podporované transporty
1. WebSocket (preferred)
2. Server-Sent Events
3. Long Polling (fallback)

---

## Quick Start

### JavaScript/TypeScript
```javascript
// 1. Install SignalR client
npm install @microsoft/signalr

// 2. Connect to hub
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/chat")
    .withAutomaticReconnect()
    .build();

// 3. Setup event handlers
connection.on("ReceiveMessage", (message) => {
    console.log("New message:", message);
});

connection.on("UserJoined", (nickname, roomId) => {
    console.log(`${nickname} joined room ${roomId}`);
});

// 4. Start connection
await connection.start();
console.log("Connected to ChatHub");

// 5. Join room
await connection.invoke("JoinRoom", roomId, "alice");

// 6. Send message
await connection.invoke("SendMessage", roomId, "Hello everyone!");
```

---

## Hub Methods

## Client → Server (Invoke)

### JoinRoom
Vstoupí do chatovací místnosti.

**Method:** `JoinRoom(string roomId, string nickname)`

**Parameters:**
- `roomId`: GUID místnosti jako string
- `nickname`: nickname uživatele

**Returns:** `Task` (void)

**Behavior:**
1. Validuje existenci uživatele a místnosti
2. Přidá uživatele do SignalR group (roomId)
3. Automaticky opustí předchozí místnost (pokud existuje)
4. Vytvoří system message "UserJoined"
5. Odešle historii zpráv volajícímu (`RoomHistory` event)
6. Notifikuje ostatní účastníky (`UserJoined` event)

**Example:**
```javascript
await connection.invoke("JoinRoom", "3fa85f64-5717-4562-b3fc-2c963f66afa6", "alice");
```

**Errors:**
```javascript
try {
  await connection.invoke("JoinRoom", roomId, nickname);
} catch (err) {
  console.error("Failed to join room:", err);
  // err.message obsahuje HubException message
}
```

---

### LeaveRoom
Opustí chatovací místnost.

**Method:** `LeaveRoom(string roomId, string nickname)`

**Parameters:**
- `roomId`: GUID místnosti jako string
- `nickname`: nickname uživatele

**Returns:** `Task` (void)

**Behavior:**
1. Odstraní uživatele ze SignalR group
2. Vytvoří system message "UserLeft"
3. Notifikuje ostatní účastníky (`UserLeft` event)

**Example:**
```javascript
await connection.invoke("LeaveRoom", roomId, "alice");
```

---

### SendMessage
Odešle zprávu do místnosti.

**Method:** `SendMessage(string roomId, string message)`

**Parameters:**
- `roomId`: GUID místnosti jako string
- `message`: obsah zprávy (max 1000 znaků)

**Returns:** `Task` (void)

**Behavior:**
1. Validuje, že odesílatel je účastník místnosti
2. Vytvoří Message objekt
3. Uloží do historie
4. Broadcast všem účastníkům místnosti (`ReceiveMessage` event)

**Example:**
```javascript
await connection.invoke("SendMessage", roomId, "Hello everyone!");
```

**Note:** Author (nickname) je automaticky určen z connection context, takže není potřeba ho předávat.

---

## Server → Client (Events)

### ReceiveMessage
Přijme novou zprávu v místnosti.

**Event:** `ReceiveMessage(MessageResponse message)`

**MessageResponse:**
```typescript
interface MessageResponse {
  id: string;              // GUID
  roomId: string;          // GUID
  author: string;          // nickname nebo "SYSTEM"
  content: string;
  type: "UserMessage" | "UserJoined" | "UserLeft";
  timestamp: string;       // ISO 8601
}
```

**Example:**
```javascript
connection.on("ReceiveMessage", (message) => {
  const messageElement = document.createElement("div");
  messageElement.className = message.type === "UserMessage" ? "user-message" : "system-message";
  messageElement.textContent = `${message.author}: ${message.content}`;
  document.getElementById("messages").appendChild(messageElement);
});
```

---

### UserJoined
Notifikace o vstupu uživatele do místnosti.

**Event:** `UserJoined(string nickname, string roomId)`

**Parameters:**
- `nickname`: nickname uživatele, který vstoupil
- `roomId`: GUID místnosti

**Example:**
```javascript
connection.on("UserJoined", (nickname, roomId) => {
  console.log(`${nickname} joined the room`);
  updateParticipantList();
});
```

**Note:** Tento event je odeslán pouze existujícím účastníkům, ne volajícímu (použijte `RoomHistory` pro initial load).

---

### UserLeft
Notifikace o odchodu uživatele z místnosti.

**Event:** `UserLeft(string nickname, string roomId)`

**Parameters:**
- `nickname`: nickname uživatele, který odešel
- `roomId`: GUID místnosti

**Example:**
```javascript
connection.on("UserLeft", (nickname, roomId) => {
  console.log(`${nickname} left the room`);
  updateParticipantList();
});
```

---

### RoomHistory
Přijme historii zpráv po vstupu do místnosti.

**Event:** `RoomHistory(MessageResponse[] messages)`

**Parameters:**
- `messages`: pole MessageResponse objektů seřazených chronologicky

**Example:**
```javascript
connection.on("RoomHistory", (messages) => {
  const messagesContainer = document.getElementById("messages");
  messagesContainer.innerHTML = ""; // Clear

  messages.forEach(message => {
    const msgElement = createMessageElement(message);
    messagesContainer.appendChild(msgElement);
  });

  scrollToBottom();
});
```

**Note:** Tento event je odeslán pouze volajícímu po úspěšném `JoinRoom`.

---

### RoomCreated
Notifikace o vytvoření nové místnosti.

**Event:** `RoomCreated(RoomResponse room)`

**RoomResponse:**
```typescript
interface RoomResponse {
  id: string;              // GUID
  name: string;
  description: string;
  createdBy: string;       // nickname
  createdAt: string;       // ISO 8601
  participantCount: number;
}
```

**Example:**
```javascript
connection.on("RoomCreated", (room) => {
  console.log(`New room created: ${room.name}`);
  addRoomToList(room);
});
```

**Note:** Tento event je broadcast všem připojeným klientům.

---

### RoomDeleted
Notifikace o smazání místnosti.

**Event:** `RoomDeleted(string roomId)`

**Parameters:**
- `roomId`: GUID smazané místnosti

**Example:**
```javascript
connection.on("RoomDeleted", (roomId) => {
  console.log(`Room ${roomId} was deleted`);
  removeRoomFromList(roomId);

  // Pokud jsme byli v této místnosti, přesměrovat
  if (currentRoomId === roomId) {
    navigateToRoomList();
  }
});
```

---

## Connection Lifecycle

### Connection States
```javascript
connection.onclose((error) => {
  console.log("Connection closed", error);
  // Reconnect logic
});

connection.onreconnecting((error) => {
  console.log("Reconnecting...", error);
  showReconnectingUI();
});

connection.onreconnected((connectionId) => {
  console.log("Reconnected", connectionId);
  hideReconnectingUI();
  // Re-join rooms if needed
});
```

### Automatic Reconnection
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/chat")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry delays
    .build();
```

### Manual Reconnection
```javascript
async function startConnection() {
  try {
    await connection.start();
    console.log("Connected");
  } catch (err) {
    console.error("Connection failed:", err);
    setTimeout(startConnection, 5000); // Retry after 5s
  }
}

connection.onclose(async () => {
  await startConnection();
});
```

---

## Error Handling

### HubException
```javascript
try {
  await connection.invoke("JoinRoom", roomId, nickname);
} catch (err) {
  if (err.message.includes("User not found")) {
    // Handle user not found
  } else if (err.message.includes("Room not found")) {
    // Handle room not found
  } else {
    // Generic error
    console.error("Hub error:", err);
  }
}
```

### Connection Errors
```javascript
connection.start()
  .catch(err => {
    if (err.message.includes("Failed to complete negotiation")) {
      console.error("Server is not reachable");
    } else {
      console.error("Connection error:", err);
    }
  });
```

---

## Complete Examples

### JavaScript Client

```javascript
import * as signalR from "@microsoft/signalr";

class ChatClient {
  constructor(hubUrl, nickname) {
    this.hubUrl = hubUrl;
    this.nickname = nickname;
    this.currentRoomId = null;
    this.connection = null;
  }

  async connect() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();

    // Setup event handlers
    this.connection.on("ReceiveMessage", (message) => this.onMessage(message));
    this.connection.on("UserJoined", (nickname, roomId) => this.onUserJoined(nickname, roomId));
    this.connection.on("UserLeft", (nickname, roomId) => this.onUserLeft(nickname, roomId));
    this.connection.on("RoomHistory", (messages) => this.onRoomHistory(messages));
    this.connection.on("RoomCreated", (room) => this.onRoomCreated(room));
    this.connection.on("RoomDeleted", (roomId) => this.onRoomDeleted(roomId));

    // Connection lifecycle
    this.connection.onclose(() => console.log("Disconnected"));
    this.connection.onreconnecting(() => console.log("Reconnecting..."));
    this.connection.onreconnected(() => {
      console.log("Reconnected");
      if (this.currentRoomId) {
        this.joinRoom(this.currentRoomId); // Re-join room
      }
    });

    await this.connection.start();
    console.log("Connected to ChatHub");
  }

  async joinRoom(roomId) {
    try {
      await this.connection.invoke("JoinRoom", roomId, this.nickname);
      this.currentRoomId = roomId;
      console.log(`Joined room ${roomId}`);
    } catch (err) {
      console.error("Failed to join room:", err);
      throw err;
    }
  }

  async leaveRoom() {
    if (!this.currentRoomId) return;

    try {
      await this.connection.invoke("LeaveRoom", this.currentRoomId, this.nickname);
      this.currentRoomId = null;
      console.log("Left room");
    } catch (err) {
      console.error("Failed to leave room:", err);
    }
  }

  async sendMessage(content) {
    if (!this.currentRoomId) {
      throw new Error("Not in a room");
    }

    try {
      await this.connection.invoke("SendMessage", this.currentRoomId, content);
    } catch (err) {
      console.error("Failed to send message:", err);
      throw err;
    }
  }

  async disconnect() {
    if (this.currentRoomId) {
      await this.leaveRoom();
    }
    await this.connection.stop();
  }

  // Event handlers (override these)
  onMessage(message) {
    console.log("Message:", message);
  }

  onUserJoined(nickname, roomId) {
    console.log(`${nickname} joined`);
  }

  onUserLeft(nickname, roomId) {
    console.log(`${nickname} left`);
  }

  onRoomHistory(messages) {
    console.log("Room history:", messages);
  }

  onRoomCreated(room) {
    console.log("Room created:", room);
  }

  onRoomDeleted(roomId) {
    console.log("Room deleted:", roomId);
  }
}

// Usage
const client = new ChatClient("http://localhost:5000/hubs/chat", "alice");
await client.connect();
await client.joinRoom("room-id-here");
await client.sendMessage("Hello!");
```

---

### C# Client

```csharp
using Microsoft.AspNetCore.SignalR.Client;

public class ChatClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly string _nickname;
    private string? _currentRoomId;

    public ChatClient(string hubUrl, string nickname)
    {
        _nickname = nickname;
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        _connection.On<MessageResponse>("ReceiveMessage", OnMessage);
        _connection.On<string, string>("UserJoined", OnUserJoined);
        _connection.On<string, string>("UserLeft", OnUserLeft);
        _connection.On<List<MessageResponse>>("RoomHistory", OnRoomHistory);
        _connection.On<RoomResponse>("RoomCreated", OnRoomCreated);
        _connection.On<string>("RoomDeleted", OnRoomDeleted);

        _connection.Closed += async (error) =>
        {
            Console.WriteLine($"Connection closed: {error?.Message}");
            await Task.Delay(5000);
            await ConnectAsync();
        };
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _connection.StartAsync();
            Console.WriteLine("Connected to ChatHub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            throw;
        }
    }

    public async Task JoinRoomAsync(string roomId)
    {
        try
        {
            await _connection.InvokeAsync("JoinRoom", roomId, _nickname);
            _currentRoomId = roomId;
            Console.WriteLine($"Joined room {roomId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to join room: {ex.Message}");
            throw;
        }
    }

    public async Task LeaveRoomAsync()
    {
        if (_currentRoomId == null) return;

        try
        {
            await _connection.InvokeAsync("LeaveRoom", _currentRoomId, _nickname);
            _currentRoomId = null;
            Console.WriteLine("Left room");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to leave room: {ex.Message}");
        }
    }

    public async Task SendMessageAsync(string content)
    {
        if (_currentRoomId == null)
            throw new InvalidOperationException("Not in a room");

        try
        {
            await _connection.InvokeAsync("SendMessage", _currentRoomId, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send message: {ex.Message}");
            throw;
        }
    }

    // Event handlers (virtual for overriding)
    protected virtual void OnMessage(MessageResponse message)
    {
        Console.WriteLine($"[{message.Author}]: {message.Content}");
    }

    protected virtual void OnUserJoined(string nickname, string roomId)
    {
        Console.WriteLine($"{nickname} joined the room");
    }

    protected virtual void OnUserLeft(string nickname, string roomId)
    {
        Console.WriteLine($"{nickname} left the room");
    }

    protected virtual void OnRoomHistory(List<MessageResponse> messages)
    {
        Console.WriteLine($"Received {messages.Count} historical messages");
    }

    protected virtual void OnRoomCreated(RoomResponse room)
    {
        Console.WriteLine($"New room: {room.Name}");
    }

    protected virtual void OnRoomDeleted(string roomId)
    {
        Console.WriteLine($"Room {roomId} was deleted");
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveRoomAsync();
        await _connection.DisposeAsync();
    }
}

// Usage
await using var client = new ChatClient("http://localhost:5000/hubs/chat", "alice");
await client.ConnectAsync();
await client.JoinRoomAsync("room-id-here");
await client.SendMessageAsync("Hello!");
```

---

## Best Practices

### 1. Connection Management
```javascript
// Dobrý přístup - reuse connection
const connection = createConnection();
await connection.start();

// Špatný přístup - nová connection pro každý call
// ❌ DON'T DO THIS
async function sendMessage(msg) {
  const conn = createConnection();
  await conn.start();
  await conn.invoke("SendMessage", roomId, msg);
  await conn.stop();
}
```

### 2. Event Handling
```javascript
// Registrovat handlers PŘED start()
connection.on("ReceiveMessage", handleMessage);
connection.on("UserJoined", handleUserJoined);

await connection.start(); // ✅ Correct order
```

### 3. State Management
```javascript
class ChatState {
  constructor() {
    this.currentRoom = null;
    this.messages = [];
    this.participants = new Set();
  }

  // Update state based on events
  handleRoomHistory(messages) {
    this.messages = messages;
  }

  handleReceiveMessage(message) {
    this.messages.push(message);
  }

  handleUserJoined(nickname) {
    this.participants.add(nickname);
  }

  handleUserLeft(nickname) {
    this.participants.delete(nickname);
  }
}
```

### 4. Error Boundaries
```javascript
async function safeInvoke(method, ...args) {
  try {
    return await connection.invoke(method, ...args);
  } catch (err) {
    console.error(`Hub method ${method} failed:`, err);
    showErrorToUser(err.message);
    throw err;
  }
}
```

---

## Performance Considerations

### Connection Pooling
- Reuse single connection per user
- Don't create new connections for each action

### Message Batching
```javascript
// Pokud posíláte mnoho zpráv rychle za sebou
const messageQueue = [];
let flushTimeout;

function queueMessage(content) {
  messageQueue.push(content);

  clearTimeout(flushTimeout);
  flushTimeout = setTimeout(flushMessages, 100); // Batch every 100ms
}

async function flushMessages() {
  const batch = [...messageQueue];
  messageQueue.length = 0;

  for (const msg of batch) {
    await connection.invoke("SendMessage", roomId, msg);
  }
}
```

### Selective Event Subscription
```javascript
// Subscribe pouze k events, které potřebujete
if (userWantsNotifications) {
  connection.on("UserJoined", handleUserJoined);
  connection.on("UserLeft", handleUserLeft);
}
```

---

## Troubleshooting

### Connection Fails
```
Error: Failed to complete negotiation with the server
```
**Solution:** Ověřte, že server běží a CORS je správně nakonfigurován.

### Hub Method Not Found
```
HubException: Failed to invoke 'JoinRoom' due to an error on the server
```
**Solution:** Ověřte správnost názvu metody a parametrů.

### Events Not Received
**Check:**
1. Event handler je registrován před `start()`
2. Název eventu přesně odpovídá server-side
3. SignalR group membership (musíte být v roomě)

---

## Further Reading

- **REST API Manual:** `REST-API-MANUAL.md`
- **Quick Start Guide:** `QUICKSTART.md`
- **SignalR Documentation:** https://docs.microsoft.com/aspnet/core/signalr

---

**Support:** Pro otázky a problémy vytvořte issue na projektu.
