using System.Diagnostics;
using GameSessionService.Api.Models;
using GameSessionService.Api.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameSessionService.Api.Services;

/// <summary>
/// Service implementation for session management with caching and concurrency safety
/// </summary>
public class SessionService : ISessionService
{
    private readonly ISessionRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionService> _logger;
    private readonly SemaphoreSlim _createLock = new(1, 1); // Ensures only one creation at a time per key

    // Cache configuration
    private const int CacheExpirationSeconds = 60;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromSeconds(CacheExpirationSeconds);

    public SessionService(
        ISessionRepository repository,
        IMemoryCache cache,
        ILogger<SessionService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new game session with idempotency checks and concurrency safety
    /// </summary>
    public async Task<SessionResponse> CreateSessionAsync(CreateSessionRequest request, string correlationId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Creating session for PlayerId: {PlayerId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                request.PlayerId, request.GameId, correlationId);

            // Use semaphore to ensure thread-safe creation
            await _createLock.WaitAsync();
            try
            {
                // Double-check pattern: Verify no session was created while waiting
                // This is the critical section where we check and create atomically
                var existingSession = await _repository.GetActiveSessionAsync(request.PlayerId, request.GameId);
                if (existingSession != null)
                {
                    _logger.LogInformation(
                        "Active session already exists - returning existing session. SessionId: {SessionId}, PlayerId: {PlayerId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                        existingSession.SessionId, request.PlayerId, request.GameId, correlationId);
                    
                    // Idempotency: Return existing session instead of creating duplicate
                    return new SessionResponse
                    {
                        SessionId = existingSession.SessionId,
                        StartedAt = existingSession.StartedAt,
                        Status = existingSession.Status.ToString()
                    };
                }

                // Generate unique session ID
                var sessionId = GenerateSessionId();

                // Create session entity
                var session = new Session
                {
                    SessionId = sessionId,
                    PlayerId = request.PlayerId,
                    GameId = request.GameId,
                    StartedAt = DateTime.UtcNow,
                    Status = SessionStatus.Active
                };

                // Create the session
                var createdSession = await _repository.CreateAsync(session);

                _logger.LogInformation(
                    "Session created successfully. SessionId: {SessionId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                    createdSession.SessionId, correlationId, stopwatch.ElapsedMilliseconds);

                return new SessionResponse
                {
                    SessionId = createdSession.SessionId,
                    StartedAt = createdSession.StartedAt,
                    Status = createdSession.Status.ToString()
                };
            }
            finally
            {
                _createLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating session for PlayerId: {PlayerId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                request.PlayerId, request.GameId, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a session by ID with caching support
    /// First lookup reads from data source, subsequent requests return from cache
    /// </summary>
    public async Task<(SessionResponse? session, bool fromCache)> GetSessionAsync(string sessionId, string correlationId)
    {
        var cacheKey = $"session:{sessionId}";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Try to get from cache first
            if (_cache.TryGetValue(cacheKey, out SessionResponse? cachedSession))
            {
                _logger.LogInformation(
                    "Session retrieved from cache. SessionId: {SessionId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                    sessionId, correlationId, stopwatch.ElapsedMilliseconds);
                
                return (cachedSession, true);
            }

            // Cache miss - read from repository
            _logger.LogInformation(
                "Cache miss - retrieving session from repository. SessionId: {SessionId}, CorrelationId: {CorrelationId}",
                sessionId, correlationId);

            var session = await _repository.GetByIdAsync(sessionId);

            if (session == null)
            {
                _logger.LogWarning(
                    "Session not found. SessionId: {SessionId}, CorrelationId: {CorrelationId}",
                    sessionId, correlationId);
                return (null, false);
            }

            var response = new SessionResponse
            {
                SessionId = session.SessionId,
                StartedAt = session.StartedAt,
                Status = session.Status.ToString()
            };

            // Store in cache with expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration,
                SlidingExpiration = null, // Use absolute expiration only
                Size = 1 // Required when SizeLimit is set on MemoryCache
            };

            _cache.Set(cacheKey, response, cacheOptions);

            _logger.LogInformation(
                "Session retrieved from repository and cached. SessionId: {SessionId}, CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                sessionId, correlationId, stopwatch.ElapsedMilliseconds);

            return (response, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving session. SessionId: {SessionId}, CorrelationId: {CorrelationId}",
                sessionId, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Generates a unique session identifier
    /// </summary>
    private static string GenerateSessionId()
    {
        return $"SESS_{Guid.NewGuid():N}";
    }
}

