# Phase 0: Project Setup

## Cíl fáze
Vytvořit základní infrastrukturu projektu včetně ASP.NET Core Web API a SignalR dependencies.

## Prerekvizity
- .NET 8.0 SDK
- Visual Studio Code nebo Visual Studio 2022

## Sub-fáze

### 0.1: Vytvoření ASP.NET Core Web API projektu
**Akce:**
- Vytvořit solution file
- Vytvořit Web API projekt: `ChatServer`
- Nakonfigurovat `launchSettings.json` (HTTP port 5000)
- Přidat základní Program.cs s Swagger

**Očekávaný výstup:**
- Spustitelná Web API aplikace
- Swagger UI dostupné na `/swagger`

**Příkazy:**
```bash
dotnet new sln -n ChatServer
dotnet new webapi -n ChatServer -o src/ChatServer
dotnet sln add src/ChatServer/ChatServer.csproj
```

### 0.2: Přidání SignalR dependencies
**Akce:**
- SignalR je součástí ASP.NET Core, ale nakonfigurovat CORS
- Přidat SignalR services do DI containeru
- Nakonfigurovat endpoint routing

**Očekávaný výstup:**
- SignalR ready pro použití v dalších fázích

### 0.3: Vytvoření základní struktury projektu
**Akce:**
- Vytvořit složky:
  - `Controllers/` - REST API controllers
  - `Models/` - Domain models & DTOs
  - `Services/` - Business logic
  - `Hubs/` - SignalR hubs
  - `Storage/` - In-memory storage
- Vytvořit base interfaces a abstrakce

**Očekávaný výstup:**
```
src/ChatServer/
├── Controllers/
├── Models/
├── Services/
├── Hubs/
├── Storage/
├── Program.cs
└── ChatServer.csproj
```

### 0.4: Vytvoření test projektu
**Akce:**
- Vytvořit xUnit test projekt: `ChatServer.Tests`
- Přidat reference na hlavní projekt
- Přidat packages: xUnit, Moq, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing
- Vytvořit base test fixtures

**Příkazy:**
```bash
dotnet new xunit -n ChatServer.Tests -o src/ChatServer.Tests
dotnet add src/ChatServer.Tests reference src/ChatServer/ChatServer.csproj
dotnet sln add src/ChatServer.Tests/ChatServer.Tests.csproj
```

**Očekávaný výstup:**
- Test projekt ready pro unit a integration testy

### 0.5: Konfigurace CORS a middleware
**Akce:**
- Nakonfigurovat CORS policy pro lokální vývoj (allowAll)
- Nakonfigurovat JSON serialization options
- Přidat exception handling middleware

**Očekávaný výstup:**
- CORS povolený pro all origins (development only)
- Globální exception handler

### 0.6: Vytvoření in-memory storage infrastruktury
**Akce:**
- Vytvořit `IDataStore` interface
- Implementovat `InMemoryDataStore` s ConcurrentDictionary
- Registrovat jako singleton v DI

**Soubory:**
- `Storage/IDataStore.cs`
- `Storage/InMemoryDataStore.cs`

**Očekávaný výstup:**
- Thread-safe in-memory storage ready

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Aplikace se spustí na http://localhost:5000
- [ ] Swagger UI je dostupné
- [ ] Health check endpoint `/health` vrací 200 OK
- [ ] CORS je nakonfigurován

### Testovací kritéria
- [ ] Test projekt se zkompiluje
- [ ] Základní smoke test prochází (aplikace se spustí)
- [ ] InMemoryDataStore unit testy prochází

### Dokumentační kritéria
- [ ] README.md obsahuje instrukce jak spustit projekt
- [ ] appsettings.json je zdokumentován

## Tests

### 0.T1: Smoke test
```csharp
[Fact]
public async Task Application_Starts_Successfully()
{
    // Arrange
    await using var application = new WebApplicationFactory<Program>();
    var client = application.CreateClient();

    // Act
    var response = await client.GetAsync("/health");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### 0.T2: InMemoryDataStore thread-safety test
```csharp
[Fact]
public async Task DataStore_Is_ThreadSafe()
{
    // Test concurrent writes to ConcurrentDictionary
}
```

## Následující fáze
Po dokončení Phase 0 přejít na **Phase 1: User Management**

## Časový odhad
**2-3 hodiny** (včetně testování)
