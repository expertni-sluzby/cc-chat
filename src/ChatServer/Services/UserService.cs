using System.Text.RegularExpressions;
using ChatServer.Models;
using ChatServer.Storage;

namespace ChatServer.Services;

/// <summary>
/// Service for user management operations
/// </summary>
public class UserService : IUserService
{
    private readonly IDataStore _dataStore;
    private static readonly Regex NicknameRegex = new(@"^[a-zA-Z0-9_]{3,20}$", RegexOptions.Compiled);

    public UserService(IDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public Task<User?> RegisterUserAsync(string nickname)
    {
        // Validate nickname format
        if (!NicknameRegex.IsMatch(nickname))
        {
            return Task.FromResult<User?>(null);
        }

        // Check if nickname is already taken (case-insensitive)
        if (_dataStore.UserExists(nickname))
        {
            return Task.FromResult<User?>(null);
        }

        // Create new user
        var user = new User
        {
            Nickname = nickname, // Preserve original casing
            RegisteredAt = DateTime.UtcNow,
            IsOnline = false,
            CurrentRoomId = null
        };

        // Try to add user to store
        var success = _dataStore.TryAddUser(nickname, user);

        return Task.FromResult(success ? user : null);
    }

    public Task<User?> LoginUserAsync(string nickname)
    {
        // Try to get user from store
        if (!_dataStore.TryGetUser(nickname, out var user))
        {
            return Task.FromResult<User?>(null);
        }

        // Update online status
        user!.IsOnline = true;
        _dataStore.TryUpdateUser(nickname, user);

        return Task.FromResult<User?>(user);
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        var users = _dataStore.GetAllUsers();
        return Task.FromResult(users);
    }

    public Task<User?> GetUserByNicknameAsync(string nickname)
    {
        _dataStore.TryGetUser(nickname, out var user);
        return Task.FromResult(user);
    }

    public Task<bool> IsNicknameAvailableAsync(string nickname)
    {
        var exists = _dataStore.UserExists(nickname);
        return Task.FromResult(!exists);
    }
}
