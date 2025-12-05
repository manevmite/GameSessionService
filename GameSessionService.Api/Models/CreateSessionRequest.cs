namespace GameSessionService.Api.Models;

/// <summary>
/// Request model for creating a new game session
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// Identifier of the player starting the session
    /// </summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the game to be played
    /// </summary>
    public string GameId { get; set; } = string.Empty;
}

