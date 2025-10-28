using ChatServer.Models;

namespace ChatServer.Services;

/// <summary>
/// Service interface for user management operations
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Registers a new user with the given nickname
    /// </summary>
    /// <param name="nickname">The nickname to register</param>
    /// <returns>The created user or null if nickname is already taken</returns>
    Task<User?> RegisterUserAsync(string nickname);

    /// <summary>
    /// Logs in an existing user and sets them as online
    /// </summary>
    /// <param name="nickname">The nickname to log in</param>
    /// <returns>The user or null if not found</returns>
    Task<User?> LoginUserAsync(string nickname);

    /// <summary>
    /// Gets all registered users
    /// </summary>
    /// <returns>Collection of all users</returns>
    Task<IEnumerable<User>> GetAllUsersAsync();

    /// <summary>
    /// Finds a user by their nickname
    /// </summary>
    /// <param name="nickname">The nickname to search for</param>
    /// <returns>The user or null if not found</returns>
    Task<User?> GetUserByNicknameAsync(string nickname);

    /// <summary>
    /// Checks if a nickname is available for registration
    /// </summary>
    /// <param name="nickname">The nickname to check</param>
    /// <returns>True if available, false if taken</returns>
    Task<bool> IsNicknameAvailableAsync(string nickname);
}
