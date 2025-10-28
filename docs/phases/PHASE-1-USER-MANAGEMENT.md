# Phase 1: User Management

## Cíl fáze
Implementovat kompletní správu uživatelů včetně registrace, přihlášení a získání seznamu uživatelů.

## Prerekvizity
- Dokončená Phase 0 (projekt setup)
- Funkční InMemoryDataStore

## Sub-fáze

### 1.1: Vytvoření User modelu a DTOs
**Akce:**
- Vytvořit `Models/User.cs` - domain model
- Vytvořit `Models/DTOs/RegisterUserRequest.cs`
- Vytvořit `Models/DTOs/LoginUserRequest.cs`
- Vytvořit `Models/DTOs/UserResponse.cs`

**User model:**
```csharp
public class User
{
    public string Nickname { get; set; }
    public DateTime RegisteredAt { get; set; }
    public bool IsOnline { get; set; }
    public string? CurrentRoomId { get; set; }
}
```

**Očekávaný výstup:**
- Kompletní domain model pro User
- DTO objekty pro API komunikaci

### 1.2: Vytvoření UserService
**Akce:**
- Vytvořit `Services/IUserService.cs` interface
- Implementovat `Services/UserService.cs`
- Registrovat v DI jako scoped service

**Metody:**
- `Task<User?> RegisterUserAsync(string nickname)` - registruje nového uživatele
- `Task<User?> LoginUserAsync(string nickname)` - přihlásí existujícího uživatele
- `Task<IEnumerable<User>> GetAllUsersAsync()` - vrátí všechny uživatele
- `Task<User?> GetUserByNicknameAsync(string nickname)` - najde uživatele
- `Task<bool> IsNicknameAvailableAsync(string nickname)` - zkontroluje dostupnost

**Business logika:**
- Nickname musí být unique
- Nickname: 3-20 znaků, alphanumeric + underscore
- Case-insensitive pro porovnání, case-preserving pro zobrazení
- Login nastaví IsOnline = true

**Očekávaný výstup:**
- Plně funkční UserService s thread-safe operacemi

### 1.3: Vytvoření UsersController
**Akce:**
- Vytvořit `Controllers/UsersController.cs`
- Implementovat REST endpoints

**Endpoints:**
```
POST   /api/users/register
  Body: { "nickname": "string" }
  Response: 201 Created + UserResponse
  Errors: 400 (invalid), 409 (exists)

POST   /api/users/login
  Body: { "nickname": "string" }
  Response: 200 OK + UserResponse
  Errors: 400 (invalid), 404 (not found)

GET    /api/users
  Response: 200 OK + UserResponse[]
```

**Očekávaný výstup:**
- Funkční REST API pro user management

### 1.4: Validace vstupů
**Akce:**
- Přidat FluentValidation NuGet package
- Vytvořit `Validators/RegisterUserRequestValidator.cs`
- Vytvořit `Validators/LoginUserRequestValidator.cs`
- Zaregistrovat validators v DI

**Validační pravidla:**
- Nickname: NotEmpty, Length(3-20), Matches("^[a-zA-Z0-9_]+$")

**Očekávaný výstup:**
- Automatická validace vstupů před zpracováním

### 1.5: Error handling a response formatting
**Akce:**
- Vytvořit `Models/DTOs/ErrorResponse.cs`
- Vytvořit `Middleware/ExceptionHandlingMiddleware.cs`
- Jednotný formát error responses

**ErrorResponse:**
```csharp
public class ErrorResponse
{
    public string Error { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Očekávaný výstup:**
- Konzistentní error handling napříč API

## Definice hotovosti (Definition of Done)

### Funkční kritéria
- [ ] Lze zaregistrovat nového uživatele
- [ ] Nelze zaregistrovat duplicitní nickname
- [ ] Lze se přihlásit s existujícím nicknamen
- [ ] Nelze se přihlásit s neexistujícím nicknamen
- [ ] Lze získat seznam všech uživatelů
- [ ] Validace vstupů funguje správně

### Testovací kritéria
- [ ] Unit testy pro UserService (100% coverage)
- [ ] Integration testy pro UsersController
- [ ] Validator testy
- [ ] Thread-safety testy

### Dokumentační kritéria
- [ ] API endpoints zdokumentovány v Swagger
- [ ] XML komentáře na public members

## Tests

### 1.T1: UserService - úspěšná registrace
```csharp
[Fact]
public async Task RegisterUser_ValidNickname_ReturnsUser()
{
    // Arrange
    var service = CreateUserService();
    var nickname = "testuser";

    // Act
    var result = await service.RegisterUserAsync(nickname);

    // Assert
    result.Should().NotBeNull();
    result!.Nickname.Should().Be(nickname);
    result.IsOnline.Should().BeFalse();
}
```

### 1.T2: UserService - duplicitní nickname
```csharp
[Fact]
public async Task RegisterUser_DuplicateNickname_ReturnsNull()
{
    // Arrange
    var service = CreateUserService();
    await service.RegisterUserAsync("testuser");

    // Act
    var result = await service.RegisterUserAsync("testuser");

    // Assert
    result.Should().BeNull();
}
```

### 1.T3: UserService - case insensitive
```csharp
[Fact]
public async Task RegisterUser_CaseInsensitive_PreventsConflict()
{
    // Arrange
    var service = CreateUserService();
    await service.RegisterUserAsync("TestUser");

    // Act
    var result = await service.RegisterUserAsync("testuser");

    // Assert
    result.Should().BeNull();
}
```

### 1.T4: UsersController - integration test
```csharp
[Fact]
public async Task Register_ValidRequest_Returns201Created()
{
    // Arrange
    var client = CreateTestClient();
    var request = new { nickname = "testuser" };

    // Act
    var response = await client.PostAsJsonAsync("/api/users/register", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var user = await response.Content.ReadFromJsonAsync<UserResponse>();
    user!.Nickname.Should().Be("testuser");
}
```

### 1.T5: Validator test
```csharp
[Theory]
[InlineData("ab")] // too short
[InlineData("a".PadRight(21, 'a'))] // too long
[InlineData("user-name")] // invalid char
[InlineData("")] // empty
public async Task Validator_InvalidNickname_FailsValidation(string nickname)
{
    // Arrange
    var validator = new RegisterUserRequestValidator();
    var request = new RegisterUserRequest { Nickname = nickname };

    // Act
    var result = await validator.ValidateAsync(request);

    // Assert
    result.IsValid.Should().BeFalse();
}
```

### 1.T6: Thread-safety test
```csharp
[Fact]
public async Task RegisterUser_ConcurrentCalls_OnlyOneSucceeds()
{
    // Test concurrent registration with same nickname
    // Only first should succeed
}
```

## Následující fáze
Po dokončení Phase 1 přejít na **Phase 2: Room Management**

## Časový odhad
**4-5 hodin** (včetně testování)
