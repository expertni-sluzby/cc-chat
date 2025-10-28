# Chat Server - Quick Start Guide

**Čas: 20 minut**

Tento průvodce vás provede vytvořením jednoduchého chatovacího klienta od nuly do funkční aplikace.

## Prerekvizity

- Node.js 18+ nebo .NET 8.0 SDK
- Running Chat Server na http://localhost:5000

---

## Part 1: Spuštění serveru (5 minut)

### 1. Clone nebo setup projektu
```bash
cd cc-chat
dotnet restore
```

### 2. Spuštění serveru
```bash
dotnet run --project src/ChatServer
```

### 3. Ověření
Otevřete prohlížeč: http://localhost:5000/swagger

Měli byste vidět Swagger UI s dostupnými endpoints.

---

## Part 2: První chat klient přes REST API (10 minut)

Vytvoříme jednoduchou HTML stránku s JavaScript, která komunikuje přes REST API.

### 1. Vytvořte `client.html`

```html
<!DOCTYPE html>
<html>
<head>
    <title>Chat Client - REST</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 50px auto; }
        #messages { border: 1px solid #ccc; height: 400px; overflow-y: scroll; padding: 10px; }
        .message { margin: 5px 0; }
        .system { color: #888; font-style: italic; }
        input, button { padding: 10px; margin: 5px; }
    </style>
</head>
<body>
    <h1>Chat Client (REST API)</h1>

    <div id="setup">
        <input id="nickname" placeholder="Your nickname" />
        <button onclick="register()">Register & Login</button>
    </div>

    <div id="roomSetup" style="display:none;">
        <input id="roomName" placeholder="Room name" />
        <button onclick="createRoom()">Create Room</button>
        <div id="roomsList"></div>
    </div>

    <div id="chatArea" style="display:none;">
        <h2 id="currentRoom"></h2>
        <div id="messages"></div>
        <input id="messageInput" placeholder="Type a message..." />
        <button onclick="sendMessage()">Send</button>
        <button onclick="leaveRoom()">Leave Room</button>
    </div>

    <script>
        const API_URL = 'http://localhost:5000/api';
        let currentUser = null;
        let currentRoom = null;
        let pollingInterval = null;

        async function register() {
            const nickname = document.getElementById('nickname').value;
            if (!nickname) return alert('Enter nickname');

            try {
                // Register
                await fetch(`${API_URL}/users/register`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ nickname })
                });

                // Login
                const response = await fetch(`${API_URL}/users/login`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ nickname })
                });

                currentUser = await response.json();
                document.getElementById('setup').style.display = 'none';
                document.getElementById('roomSetup').style.display = 'block';
                loadRooms();
            } catch (err) {
                console.error('Registration failed:', err);
            }
        }

        async function loadRooms() {
            const response = await fetch(`${API_URL}/rooms`);
            const rooms = await response.json();

            const list = document.getElementById('roomsList');
            list.innerHTML = '<h3>Available Rooms</h3>';

            rooms.forEach(room => {
                const div = document.createElement('div');
                div.innerHTML = `
                    <strong>${room.name}</strong> - ${room.description}
                    (${room.participantCount} participants)
                    <button onclick="joinRoom('${room.id}', '${room.name}')">Join</button>
                `;
                list.appendChild(div);
            });
        }

        async function createRoom() {
            const name = document.getElementById('roomName').value;
            if (!name) return alert('Enter room name');

            const response = await fetch(`${API_URL}/rooms`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name,
                    description: 'Created via quick start',
                    createdBy: currentUser.nickname
                })
            });

            const room = await response.json();
            joinRoom(room.id, room.name);
        }

        async function joinRoom(roomId, roomName) {
            await fetch(`${API_URL}/rooms/${roomId}/join`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ nickname: currentUser.nickname })
            });

            currentRoom = { id: roomId, name: roomName };
            document.getElementById('roomSetup').style.display = 'none';
            document.getElementById('chatArea').style.display = 'block';
            document.getElementById('currentRoom').textContent = roomName;

            loadMessages();
            startPolling();
        }

        async function loadMessages() {
            const response = await fetch(`${API_URL}/rooms/${currentRoom.id}/messages`);
            const messages = await response.json();

            const container = document.getElementById('messages');
            container.innerHTML = '';

            messages.forEach(msg => {
                const div = document.createElement('div');
                div.className = msg.type === 'UserMessage' ? 'message' : 'message system';
                div.textContent = `${msg.author}: ${msg.content}`;
                container.appendChild(div);
            });

            container.scrollTop = container.scrollHeight;
        }

        function startPolling() {
            // Poll for new messages every 2 seconds (not ideal, but works for demo)
            pollingInterval = setInterval(loadMessages, 2000);
        }

        async function sendMessage() {
            const input = document.getElementById('messageInput');
            const content = input.value;
            if (!content) return;

            await fetch(`${API_URL}/rooms/${currentRoom.id}/messages`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    author: currentUser.nickname,
                    content
                })
            });

            input.value = '';
            loadMessages();
        }

        async function leaveRoom() {
            clearInterval(pollingInterval);

            await fetch(`${API_URL}/rooms/${currentRoom.id}/leave`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ nickname: currentUser.nickname })
            });

            currentRoom = null;
            document.getElementById('chatArea').style.display = 'none';
            document.getElementById('roomSetup').style.display = 'block';
            loadRooms();
        }
    </script>
</body>
</html>
```

### 2. Otevřete v prohlížeči
```bash
# Jednoduše otevřete client.html v prohlížeči
open client.html
```

### 3. Vyzkoušejte
1. Zadejte nickname a klikněte "Register & Login"
2. Vytvořte místnost nebo se připojte k existující
3. Posílejte zprávy
4. Otevřete druhou záložku prohlížeče a opakujte s jiným nicknamen

**Note:** Tento klient používá polling (každé 2 sekundy). Pro real-time pokračujte na Part 3.

---

## Part 3: Upgrade na real-time WebSocket (10 minut)

Nyní vylepšíme klienta o SignalR pro okamžité updaty.

### 1. Vytvořte `client-websocket.html`

```html
<!DOCTYPE html>
<html>
<head>
    <title>Chat Client - WebSocket</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 50px auto; }
        #messages { border: 1px solid #ccc; height: 400px; overflow-y: scroll; padding: 10px; margin: 10px 0; }
        .message { margin: 5px 0; padding: 5px; border-radius: 3px; }
        .user-message { background: #e3f2fd; }
        .system { background: #f5f5f5; font-style: italic; color: #666; }
        input, button { padding: 10px; margin: 5px; }
        .status { padding: 10px; background: #4caf50; color: white; }
        .status.disconnected { background: #f44336; }
    </style>
</head>
<body>
    <h1>Chat Client (WebSocket)</h1>
    <div id="status" class="status disconnected">Disconnected</div>

    <div id="setup">
        <input id="nickname" placeholder="Your nickname" />
        <button onclick="setup()">Start</button>
    </div>

    <div id="roomSetup" style="display:none;">
        <input id="roomName" placeholder="Room name" />
        <button onclick="createRoom()">Create Room</button>
        <div id="roomsList"></div>
    </div>

    <div id="chatArea" style="display:none;">
        <h2 id="currentRoom"></h2>
        <div id="participants"></div>
        <div id="messages"></div>
        <input id="messageInput" placeholder="Type a message..." onkeypress="if(event.key==='Enter') sendMessage()" />
        <button onclick="sendMessage()">Send</button>
        <button onclick="leaveRoom()">Leave Room</button>
    </div>

    <script>
        const API_URL = 'http://localhost:5000/api';
        const HUB_URL = 'http://localhost:5000/hubs/chat';

        let currentUser = null;
        let currentRoom = null;
        let connection = null;
        let participants = new Set();

        async function setup() {
            const nickname = document.getElementById('nickname').value;
            if (!nickname) return alert('Enter nickname');

            try {
                // Register via REST
                await fetch(`${API_URL}/users/register`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ nickname })
                });

                // Login via REST
                const response = await fetch(`${API_URL}/users/login`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ nickname })
                });

                currentUser = await response.json();

                // Connect to SignalR
                await connectToHub();

                document.getElementById('setup').style.display = 'none';
                document.getElementById('roomSetup').style.display = 'block';
                loadRooms();
            } catch (err) {
                console.error('Setup failed:', err);
                alert('Setup failed: ' + err.message);
            }
        }

        async function connectToHub() {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(HUB_URL)
                .withAutomaticReconnect()
                .build();

            // Event handlers
            connection.on("ReceiveMessage", (message) => {
                displayMessage(message);
            });

            connection.on("UserJoined", (nickname, roomId) => {
                participants.add(nickname);
                updateParticipants();
            });

            connection.on("UserLeft", (nickname, roomId) => {
                participants.delete(nickname);
                updateParticipants();
            });

            connection.on("RoomHistory", (messages) => {
                const container = document.getElementById('messages');
                container.innerHTML = '';
                messages.forEach(msg => displayMessage(msg));
            });

            connection.on("RoomCreated", (room) => {
                console.log('New room created:', room);
                loadRooms(); // Refresh room list
            });

            // Connection lifecycle
            connection.onclose(() => {
                updateStatus('disconnected');
            });

            connection.onreconnecting(() => {
                updateStatus('reconnecting');
            });

            connection.onreconnected(() => {
                updateStatus('connected');
                if (currentRoom) {
                    // Re-join room after reconnection
                    connection.invoke("JoinRoom", currentRoom.id, currentUser.nickname);
                }
            });

            await connection.start();
            updateStatus('connected');
            console.log('Connected to ChatHub');
        }

        function updateStatus(status) {
            const statusEl = document.getElementById('status');
            statusEl.className = 'status ' + status;
            statusEl.textContent = status.charAt(0).toUpperCase() + status.slice(1);
        }

        async function loadRooms() {
            const response = await fetch(`${API_URL}/rooms`);
            const rooms = await response.json();

            const list = document.getElementById('roomsList');
            list.innerHTML = '<h3>Available Rooms</h3>';

            rooms.forEach(room => {
                const div = document.createElement('div');
                div.style.margin = '10px 0';
                div.innerHTML = `
                    <strong>${room.name}</strong> - ${room.description}<br>
                    <small>${room.participantCount} participants</small>
                    <button onclick="joinRoom('${room.id}', '${room.name}')">Join</button>
                `;
                list.appendChild(div);
            });
        }

        async function createRoom() {
            const name = document.getElementById('roomName').value;
            if (!name) return alert('Enter room name');

            const response = await fetch(`${API_URL}/rooms`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name,
                    description: 'WebSocket demo room',
                    createdBy: currentUser.nickname
                })
            });

            const room = await response.json();
            document.getElementById('roomName').value = '';
            // Room will appear in list via RoomCreated event
        }

        async function joinRoom(roomId, roomName) {
            try {
                await connection.invoke("JoinRoom", roomId, currentUser.nickname);

                currentRoom = { id: roomId, name: roomName };
                participants.clear();
                participants.add(currentUser.nickname);

                document.getElementById('roomSetup').style.display = 'none';
                document.getElementById('chatArea').style.display = 'block';
                document.getElementById('currentRoom').textContent = roomName;

                // Get participant list
                const roomDetail = await fetch(`${API_URL}/rooms/${roomId}`).then(r => r.json());
                roomDetail.participants.forEach(p => participants.add(p));
                updateParticipants();

            } catch (err) {
                alert('Failed to join room: ' + err.message);
            }
        }

        async function sendMessage() {
            const input = document.getElementById('messageInput');
            const content = input.value.trim();
            if (!content) return;

            try {
                await connection.invoke("SendMessage", currentRoom.id, content);
                input.value = '';
            } catch (err) {
                alert('Failed to send message: ' + err.message);
            }
        }

        async function leaveRoom() {
            try {
                await connection.invoke("LeaveRoom", currentRoom.id, currentUser.nickname);

                currentRoom = null;
                participants.clear();

                document.getElementById('chatArea').style.display = 'none';
                document.getElementById('roomSetup').style.display = 'block';
                loadRooms();
            } catch (err) {
                console.error('Failed to leave room:', err);
            }
        }

        function displayMessage(message) {
            const container = document.getElementById('messages');
            const div = document.createElement('div');
            div.className = message.type === 'UserMessage' ? 'message user-message' : 'message system';

            const time = new Date(message.timestamp).toLocaleTimeString();
            div.innerHTML = `<strong>${message.author}</strong> <small>${time}</small><br>${message.content}`;

            container.appendChild(div);
            container.scrollTop = container.scrollHeight;
        }

        function updateParticipants() {
            const container = document.getElementById('participants');
            container.innerHTML = `<small>Participants (${participants.size}): ${Array.from(participants).join(', ')}</small>`;
        }
    </script>
</body>
</html>
```

### 2. Otevřete v prohlížeči
```bash
open client-websocket.html
```

### 3. Vyzkoušejte real-time chat
1. Otevřete 2-3 záložky prohlížeče
2. V každé použijte jiný nickname
3. Vytvořte místnost v jedné záložce
4. Připojte se k ní z ostatních záložek
5. Pište zprávy - uvidíte je OKAMŽITĚ ve všech záložkách!

---

## Co jsme se naučili

### REST API (Part 2)
✅ Registrace a login uživatelů
✅ Vytváření a prohlížení místností
✅ Odesílání a příjem zpráv
✅ Polling pro updates

### WebSocket (Part 3)
✅ SignalR connection setup
✅ Real-time message delivery
✅ Automatická reconnection
✅ Event-driven architecture
✅ System notifications (join/leave)

---

## Další kroky

### 1. Přidejte funkce
- Private messages
- User typing indicators
- Message reactions
- File uploads
- User avatars

### 2. Vylepšete UI
- Použijte React/Vue/Angular
- Material Design nebo Tailwind CSS
- Animations
- Sound notifications

### 3. Přidejte features
- Markdown support v messages
- Code highlighting
- Emoji picker
- Search in messages
- User profiles

### 4. Production ready
- Error handling
- Loading states
- Offline support
- Message persistence

---

## Resources

- **REST API Manual:** `REST-API-MANUAL.md` - kompletní dokumentace všech endpoints
- **WebSocket Manual:** `WEBSOCKET-MANUAL.md` - detailní SignalR guide
- **Implementation Plan:** `../IMPLEMENTATION-PLAN.md` - jak server funguje
- **Swagger UI:** http://localhost:5000/swagger - interaktivní API explorer

---

## Troubleshooting

### Server neběží
```bash
cd cc-chat
dotnet run --project src/ChatServer
```

### CORS errors
- Ověřte, že server běží na `localhost:5000`
- Zkontrolujte konzoli prohlížeče pro detaily

### Messages se nezobrazují
- Ověřte, že jste v místnosti (join room)
- Zkontrolujte Network tab v dev tools
- Pro WebSocket: ověřte connection status

### SignalR se nepřipojí
- Zkontrolujte, že SignalR CDN je dostupný
- Ověřte URL hubu: `http://localhost:5000/hubs/chat`

---

**Gratulujeme! Vytvořili jste svůj první real-time chat klient!**

Pro produkční aplikaci doporučujeme použít framework (React, Vue, Angular) a build tools (webpack, vite).
