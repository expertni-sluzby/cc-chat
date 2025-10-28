# Chat Server - REST API Integration Manual

**Version:** 1.0
**Last Updated:** 2025-10-28

## Přehled

Chat Server poskytuje RESTful API pro kompletní správu uživatelů, chatovacích místností a zpráv. API je postaveno na ASP.NET Core Web API a používá JSON pro komunikaci.

### Base URL
```
http://localhost:5000/api
```

### Content Type
```
Content-Type: application/json
```

### Authentication
V aktuální verzi je autentizace založena na nickname. Není potřeba token ani API key.

---

## Quick Start

### 1. Registrace uživatele
```bash
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"nickname": "alice"}'
```

### 2. Vytvoření místnosti
```bash
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"nickname": "alice"}'

curl -X POST http://localhost:5000/api/rooms \
  -H "Content-Type: application/json" \
  -d '{
    "name": "General Chat",
    "description": "Main discussion room",
    "createdBy": "alice"
  }'
```

### 3. Vstup do místnosti
```bash
curl -X POST http://localhost:5000/api/rooms/{roomId}/join \
  -H "Content-Type: application/json" \
  -d '{"nickname": "alice"}'
```

### 4. Odeslání zprávy
```bash
curl -X POST http://localhost:5000/api/rooms/{roomId}/messages \
  -H "Content-Type: application/json" \
  -d '{
    "author": "alice",
    "content": "Hello everyone!"
  }'
```

---

## API Endpoints

## Users

### Register User
Zaregistruje nového uživatele v systému.

**Endpoint:** `POST /api/users/register`

**Request Body:**
```json
{
  "nickname": "string"
}
```

**Validace:**
- `nickname`: 3-20 znaků, pouze alphanumeric a underscore
- Nickname musí být unikátní (case-insensitive)

**Response:** `201 Created`
```json
{
  "nickname": "alice",
  "registeredAt": "2025-10-28T10:30:00Z",
  "isOnline": false
}
```

**Error Responses:**
- `400 Bad Request` - neplatný nickname formát
```json
{
  "error": "Validation failed",
  "details": "Nickname must be 3-20 characters",
  "timestamp": "2025-10-28T10:30:00Z"
}
```
- `409 Conflict` - nickname již existuje
```json
{
  "error": "Nickname already exists",
  "timestamp": "2025-10-28T10:30:00Z"
}
```

---

### Login User
Přihlásí existujícího uživatele (nastaví isOnline = true).

**Endpoint:** `POST /api/users/login`

**Request Body:**
```json
{
  "nickname": "string"
}
```

**Response:** `200 OK`
```json
{
  "nickname": "alice",
  "registeredAt": "2025-10-28T10:30:00Z",
  "isOnline": true
}
```

**Error Responses:**
- `404 Not Found` - uživatel neexistuje
```json
{
  "error": "User not found",
  "timestamp": "2025-10-28T10:30:00Z"
}
```

---

### Get All Users
Vrátí seznam všech registrovaných uživatelů.

**Endpoint:** `GET /api/users`

**Response:** `200 OK`
```json
[
  {
    "nickname": "alice",
    "registeredAt": "2025-10-28T10:30:00Z",
    "isOnline": true
  },
  {
    "nickname": "bob",
    "registeredAt": "2025-10-28T10:35:00Z",
    "isOnline": false
  }
]
```

---

## Rooms

### Create Room
Vytvoří novou chatovací místnost.

**Endpoint:** `POST /api/rooms`

**Request Body:**
```json
{
  "name": "string",
  "description": "string",
  "createdBy": "string"
}
```

**Validace:**
- `name`: 3-50 znaků, povinné
- `description`: max 200 znaků, volitelné
- `createdBy`: musí být existující uživatel

**Response:** `201 Created`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "General Chat",
  "description": "Main discussion room",
  "createdBy": "alice",
  "createdAt": "2025-10-28T10:40:00Z",
  "participantCount": 0
}
```

**Error Responses:**
- `400 Bad Request` - validační chyba
- `404 Not Found` - createdBy uživatel neexistuje

---

### Get All Rooms
Vrátí seznam všech místností.

**Endpoint:** `GET /api/rooms`

**Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "General Chat",
    "description": "Main discussion room",
    "createdBy": "alice",
    "createdAt": "2025-10-28T10:40:00Z",
    "participantCount": 5
  }
]
```

---

### Get Room Detail
Vrátí detail místnosti včetně seznamu účastníků.

**Endpoint:** `GET /api/rooms/{id}`

**Response:** `200 OK`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "General Chat",
  "description": "Main discussion room",
  "createdBy": "alice",
  "createdAt": "2025-10-28T10:40:00Z",
  "participants": ["alice", "bob", "charlie"]
}
```

**Error Responses:**
- `404 Not Found` - místnost neexistuje

---

### Delete Room
Smaže místnost. Pouze tvůrce místnosti má oprávnění.

**Endpoint:** `DELETE /api/rooms/{id}?requestingUser={nickname}`

**Query Parameters:**
- `requestingUser`: nickname uživatele, který žádá o smazání

**Response:** `204 No Content`

**Error Responses:**
- `403 Forbidden` - uživatel není tvůrce místnosti
```json
{
  "error": "Only room creator can delete the room",
  "timestamp": "2025-10-28T10:40:00Z"
}
```
- `404 Not Found` - místnost neexistuje

---

### Join Room
Vstoupí do místnosti.

**Endpoint:** `POST /api/rooms/{id}/join`

**Request Body:**
```json
{
  "nickname": "string"
}
```

**Business Rules:**
- Uživatel může být pouze v jedné místnosti současně
- Pokud je již v jiné místnosti, automaticky z ní odejde
- Vytvoří se system message "UserJoined"

**Response:** `200 OK`

**Error Responses:**
- `404 Not Found` - místnost nebo uživatel neexistuje
- `400 Bad Request` - validační chyba

---

### Leave Room
Opustí místnost.

**Endpoint:** `POST /api/rooms/{id}/leave`

**Request Body:**
```json
{
  "nickname": "string"
}
```

**Business Rules:**
- Vytvoří se system message "UserLeft"

**Response:** `200 OK`

**Error Responses:**
- `404 Not Found` - místnost neexistuje
- `400 Bad Request` - uživatel není v místnosti

---

## Messages

### Send Message
Odešle zprávu do místnosti.

**Endpoint:** `POST /api/rooms/{roomId}/messages`

**Request Body:**
```json
{
  "author": "string",
  "content": "string"
}
```

**Validace:**
- `author`: musí být účastník místnosti
- `content`: 1-1000 znaků, povinné

**Response:** `201 Created`
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "roomId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "author": "alice",
  "content": "Hello everyone!",
  "type": "UserMessage",
  "timestamp": "2025-10-28T10:45:00Z"
}
```

**Error Responses:**
- `403 Forbidden` - autor není účastník místnosti
```json
{
  "error": "User is not a participant of this room",
  "timestamp": "2025-10-28T10:45:00Z"
}
```
- `404 Not Found` - místnost neexistuje
- `400 Bad Request` - validační chyba

**Note:** Zpráva je také broadcast přes WebSocket všem účastníkům místnosti.

---

### Get Room Messages
Vrátí historii zpráv z místnosti.

**Endpoint:** `GET /api/rooms/{roomId}/messages?limit={number}`

**Query Parameters:**
- `limit` (optional): počet posledních zpráv k vrácení (default: všechny)

**Response:** `200 OK`
```json
[
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "roomId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "author": "SYSTEM",
    "content": "alice vstoupil do místnosti",
    "type": "UserJoined",
    "timestamp": "2025-10-28T10:40:00Z"
  },
  {
    "id": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
    "roomId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "author": "alice",
    "content": "Hello everyone!",
    "type": "UserMessage",
    "timestamp": "2025-10-28T10:45:00Z"
  }
]
```

**Error Responses:**
- `404 Not Found` - místnost neexistuje

**Note:** Zprávy jsou seřazeny chronologicky (nejstarší první).

---

## Error Handling

### Error Response Format
Všechny error responses mají konzistentní formát:

```json
{
  "error": "Human-readable error message",
  "details": "Optional additional details",
  "timestamp": "2025-10-28T10:45:00Z"
}
```

### HTTP Status Codes

| Code | Meaning | Usage |
|------|---------|-------|
| 200 | OK | Successful GET/POST/DELETE (with body) |
| 201 | Created | Successful resource creation |
| 204 | No Content | Successful DELETE |
| 400 | Bad Request | Validation error, invalid input |
| 403 | Forbidden | Authorization error (e.g., not room creator) |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Resource already exists (e.g., duplicate nickname) |
| 500 | Internal Server Error | Unexpected server error |

---

## Best Practices

### 1. Idempotency
- `POST /api/users/login` je idempotentní - lze volat vícekrát
- `POST /api/users/register` není idempotentní - vrátí 409 při duplicitě

### 2. Error Handling
```javascript
try {
  const response = await fetch('/api/users/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nickname: 'alice' })
  });

  if (!response.ok) {
    const error = await response.json();
    console.error('Error:', error.error);
    return;
  }

  const user = await response.json();
  console.log('Registered:', user);
} catch (err) {
  console.error('Network error:', err);
}
```

### 3. Polling vs WebSocket
- REST API můžete používat pro polling (každých N sekund získat nové zprávy)
- Pro real-time aplikace doporučujeme WebSocket (viz WEBSOCKET-MANUAL.md)

### 4. Hybrid Approach
```javascript
// REST pro initial load
const messages = await fetch(`/api/rooms/${roomId}/messages`).then(r => r.json());

// WebSocket pro real-time updates
connection.on("ReceiveMessage", (newMessage) => {
  messages.push(newMessage);
  renderMessages(messages);
});
```

---

## Rate Limiting

**Aktuální stav:** Není implementováno rate limiting.

**Doporučení pro produkci:**
- User registration: max 5 requests / minute / IP
- Message sending: max 60 messages / minute / user
- API calls: max 1000 requests / hour / user

---

## Examples

### Complete Chat Session (JavaScript)

```javascript
// 1. Register and login
async function setupUser(nickname) {
  await fetch('/api/users/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nickname })
  });

  const response = await fetch('/api/users/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nickname })
  });

  return response.json();
}

// 2. Create room
async function createRoom(name, createdBy) {
  const response = await fetch('/api/rooms', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name,
      description: 'Chat room',
      createdBy
    })
  });

  return response.json();
}

// 3. Join room
async function joinRoom(roomId, nickname) {
  await fetch(`/api/rooms/${roomId}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nickname })
  });
}

// 4. Send message
async function sendMessage(roomId, author, content) {
  const response = await fetch(`/api/rooms/${roomId}/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ author, content })
  });

  return response.json();
}

// 5. Get messages
async function getMessages(roomId) {
  const response = await fetch(`/api/rooms/${roomId}/messages`);
  return response.json();
}

// Usage
const user = await setupUser('alice');
const room = await createRoom('General', 'alice');
await joinRoom(room.id, 'alice');
await sendMessage(room.id, 'alice', 'Hello!');
const messages = await getMessages(room.id);
```

---

## Testing

### Swagger UI
Pro interaktivní testování API použijte Swagger UI:
```
http://localhost:5000/swagger
```

### Postman Collection
Připravená Postman kolekce bude k dispozici v `docs/integration/postman/`

---

## Further Reading

- **WebSocket Integration:** `WEBSOCKET-MANUAL.md`
- **Quick Start Guide:** `QUICKSTART.md`
- **Architecture:** `../ARCHITECTURE.md`

---

**Support:** Pro otázky a problémy vytvořte issue na projektu.
