using GameSessionService.Api.Models;

namespace GameSessionService.Api.Services;

/// <summary>
/// Service interface for session business logic operations
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new game session with idempotency and concurrency safety
    /// </summary>
    /// <param name="request">The session creation request</param>
    /// <param name="correlationId">Correlation ID for request tracking</param>
    /// <returns>The created session response</returns>
    Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string correlationId);

    /// <summary>
    /// Retrieves a session by ID with caching support
    /// </summary>
    /// <param name="sessionId">The unique session identifier</param>
    /// <param name="correlationId">Correlation ID for request tracking</param>
    /// <returns>A tuple containing the session (if found) and cache hit status</returns>
    Task<(SessionResponse? session, bool fromCache)> GetSessionAsync(string sessionId, string correlationId);
}

