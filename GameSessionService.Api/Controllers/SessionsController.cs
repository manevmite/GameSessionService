using GameSessionService.Api.Models;
using GameSessionService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameSessionService.Api.Controllers;

/// <summary>
/// Controller for managing game sessions
/// </summary>
[ApiController]
[Route("[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new game session
    /// POST /sessions/start
    /// Implements idempotency: if an active session exists, returns it instead of creating a duplicate
    /// </summary>
    /// <param name="request">The session creation request</param>
    /// <returns>The created or existing session information</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SessionResponse>> StartSession([FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerId) || string.IsNullOrWhiteSpace(request.GameId))
        {
            return BadRequest("PlayerId and GameId are required.");
        }

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;

        try
        {
            // Service handles idempotency internally - returns existing session if found
            var response = await _sessionService.CreateSessionAsync(request, correlationId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating session, CorrelationId: {CorrelationId}", correlationId);
            return StatusCode(500, new { message = "An error occurred while creating the session." });
        }
    }

    /// <summary>
    /// Retrieves a session by its unique identifier
    /// GET /api/sessions/{sessionId}
    /// Includes cache status in response header
    /// </summary>
    /// <param name="sessionId">The unique session identifier</param>
    /// <returns>The session information if found</returns>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionResponse>> GetSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;

        try
        {
            var (session, fromCache) = await _sessionService.GetSessionAsync(sessionId, correlationId);

            if (session == null)
            {
                return NotFound(new { message = $"Session with ID {sessionId} not found." });
            }

            // Add cache status header
            Response.Headers["X-Cache"] = fromCache ? "Hit" : "Miss";

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving session, CorrelationId: {CorrelationId}", correlationId);
            return StatusCode(500, new { message = "An error occurred while retrieving the session." });
        }
    }
}

