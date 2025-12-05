namespace GameSessionService.Api.Models;

/// <summary>
/// Response model for session operations
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the session was started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Current status of the session
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

