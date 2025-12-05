using GameSessionService.Api.Models;

namespace GameSessionService.Api.Repositories;

/// <summary>
/// Repository interface for session data access operations
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Creates a new session in the data store
    /// </summary>
    /// <param name="session">The session to create</param>
    /// <returns>The created session</returns>
    Task<Session> CreateAsync(Session session);

    /// <summary>
    /// Retrieves a session by its unique identifier
    /// </summary>
    /// <param name="sessionId">The unique session identifier</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetByIdAsync(string sessionId);

    /// <summary>
    /// Checks if a session exists for the given player and game combination
    /// Used for preventing duplicate session creation
    /// </summary>
    /// <param name="playerId">The player identifier</param>
    /// <param name="gameId">The game identifier</param>
    /// <returns>True if an active session exists, false otherwise</returns>
    Task<bool> HasActiveSessionAsync(string playerId, string gameId);

    /// <summary>
    /// Retrieves an active session for the given player and game combination
    /// Used for idempotency - returning existing session instead of creating duplicate
    /// </summary>
    /// <param name="playerId">The player identifier</param>
    /// <param name="gameId">The game identifier</param>
    /// <returns>The active session if found, null otherwise</returns>
    Task<Session?> GetActiveSessionAsync(string playerId, string gameId);
}

