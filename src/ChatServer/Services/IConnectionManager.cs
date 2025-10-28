namespace ChatServer.Services;

/// <summary>
/// Manages WebSocket connections and their mapping to users
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// Adds a connection for a user
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <param name="nickname">User's nickname</param>
    Task AddConnectionAsync(string connectionId, string nickname);

    /// <summary>
    /// Removes a connection
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    Task RemoveConnectionAsync(string connectionId);

    /// <summary>
    /// Gets the nickname associated with a connection
    /// </summary>
    /// <param name="connectionId">SignalR connection ID</param>
    /// <returns>Nickname or null if not found</returns>
    Task<string?> GetNicknameByConnectionIdAsync(string connectionId);

    /// <summary>
    /// Gets all connection IDs for a user
    /// </summary>
    /// <param name="nickname">User's nickname</param>
    /// <returns>List of connection IDs</returns>
    Task<IEnumerable<string>> GetConnectionsByNicknameAsync(string nickname);

    /// <summary>
    /// Checks if a user has any active connections
    /// </summary>
    /// <param name="nickname">User's nickname</param>
    /// <returns>True if user has active connections</returns>
    Task<bool> HasConnectionsAsync(string nickname);
}
