namespace GameSessionService.Api.Models;

/// <summary>
/// Represents a game session entity
/// </summary>
public class Session
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the player who started the session
    /// </summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the game being played
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the session was created
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Current status of the session
    /// </summary>
    public SessionStatus Status { get; set; }
}

/// <summary>
/// Enumeration of possible session statuses
/// </summary>
public enum SessionStatus
{
    Active,
    Paused,
    Completed,
    Cancelled
}

