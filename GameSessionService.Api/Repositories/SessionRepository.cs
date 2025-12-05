using System.Collections.Concurrent;
using GameSessionService.Api.Models;

namespace GameSessionService.Api.Repositories;

/// <summary>
/// In-memory implementation of the session repository
/// Uses ConcurrentDictionary for thread-safe operations
/// In a production environment, this would be replaced with a database implementation
/// </summary>
public class SessionRepository : ISessionRepository
{
    // Thread-safe dictionary for storing sessions
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    
    // Index for fast lookup of active sessions by player+game combination
    private readonly ConcurrentDictionary<string, string> _activeSessionIndex = new();

    /// <summary>
    /// Creates a new session in the in-memory store
    /// </summary>
    public Task<Session> CreateAsync(Session session)
    {
        // Store the session
        _sessions.TryAdd(session.SessionId, session);
        
        // Index for duplicate prevention
        var indexKey = GetIndexKey(session.PlayerId, session.GameId);
        _activeSessionIndex.TryAdd(indexKey, session.SessionId);
        
        return Task.FromResult(session);
    }

    /// <summary>
    /// Retrieves a session by its unique identifier
    /// </summary>
    public Task<Session?> GetByIdAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <summary>
    /// Checks if an active session exists for the given player and game
    /// </summary>
    public Task<bool> HasActiveSessionAsync(string playerId, string gameId)
    {
        var indexKey = GetIndexKey(playerId, gameId);
        var hasActive = _activeSessionIndex.TryGetValue(indexKey, out var sessionId);
        
        if (hasActive && sessionId != null)
        {
            // Verify the session still exists and is active
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult(session.Status == SessionStatus.Active);
            }
            
            // Clean up stale index entry
            _activeSessionIndex.TryRemove(indexKey, out _);
        }
        
        return Task.FromResult(false);
    }

    /// <summary>
    /// Retrieves an active session for the given player and game combination
    /// </summary>
    public Task<Session?> GetActiveSessionAsync(string playerId, string gameId)
    {
        var indexKey = GetIndexKey(playerId, gameId);
        
        if (_activeSessionIndex.TryGetValue(indexKey, out var sessionId) && sessionId != null)
        {
            // Retrieve the session and verify it's still active
            if (_sessions.TryGetValue(sessionId, out var session) && session.Status == SessionStatus.Active)
            {
                return Task.FromResult<Session?>(session);
            }
            
            // Clean up stale index entry
            _activeSessionIndex.TryRemove(indexKey, out _);
        }
        
        return Task.FromResult<Session?>(null);
    }

    /// <summary>
    /// Generates a composite key for indexing sessions by player and game
    /// </summary>
    private static string GetIndexKey(string playerId, string gameId)
    {
        return $"{playerId}:{gameId}";
    }
}

