using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Thread-safe manager for WebSocket connections
/// Supports multiple connections per user (e.g., multiple tabs)
/// </summary>
public class ConnectionManager : IConnectionManager
{
    // Maps connection ID to nickname
    private readonly ConcurrentDictionary<string, string> _connectionToNickname = new();

    // Maps nickname to list of connection IDs
    private readonly ConcurrentDictionary<string, HashSet<string>> _nicknameToConnections = new();

    private readonly object _lock = new();

    public Task AddConnectionAsync(string connectionId, string nickname)
    {
        lock (_lock)
        {
            // Add to connection -> nickname mapping
            _connectionToNickname[connectionId] = nickname;

            // Add to nickname -> connections mapping
            if (!_nicknameToConnections.TryGetValue(nickname, out var connections))
            {
                connections = new HashSet<string>();
                _nicknameToConnections[nickname] = connections;
            }
            connections.Add(connectionId);
        }

        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(string connectionId)
    {
        lock (_lock)
        {
            // Remove from connection -> nickname mapping
            if (_connectionToNickname.TryRemove(connectionId, out var nickname))
            {
                // Remove from nickname -> connections mapping
                if (_nicknameToConnections.TryGetValue(nickname, out var connections))
                {
                    connections.Remove(connectionId);

                    // If no more connections for this user, remove the entry
                    if (connections.Count == 0)
                    {
                        _nicknameToConnections.TryRemove(nickname, out _);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetNicknameByConnectionIdAsync(string connectionId)
    {
        _connectionToNickname.TryGetValue(connectionId, out var nickname);
        return Task.FromResult(nickname);
    }

    public Task<IEnumerable<string>> GetConnectionsByNicknameAsync(string nickname)
    {
        if (_nicknameToConnections.TryGetValue(nickname, out var connections))
        {
            return Task.FromResult<IEnumerable<string>>(connections.ToList());
        }
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public Task<bool> HasConnectionsAsync(string nickname)
    {
        var hasConnections = _nicknameToConnections.ContainsKey(nickname);
        return Task.FromResult(hasConnections);
    }
}
