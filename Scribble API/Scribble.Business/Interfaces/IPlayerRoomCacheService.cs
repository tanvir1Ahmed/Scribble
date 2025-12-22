namespace Scribble.Business.Interfaces;

/// <summary>
/// Represents cached information about a player's room assignment
/// </summary>
public class PlayerRoomInfo
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Service for caching player-room mappings for quick lookup
/// </summary>
public interface IPlayerRoomCacheService
{
    /// <summary>
    /// Set a player's room assignment in cache
    /// </summary>
    Task SetPlayerRoomAsync(string mobileNumber, PlayerRoomInfo roomInfo);

    /// <summary>
    /// Get a player's current room assignment from cache
    /// </summary>
    Task<PlayerRoomInfo?> GetPlayerRoomAsync(string mobileNumber);

    /// <summary>
    /// Remove a player's room assignment from cache
    /// </summary>
    Task RemovePlayerRoomAsync(string mobileNumber);

    /// <summary>
    /// Check if a player is currently in any room
    /// </summary>
    Task<bool> IsPlayerInRoomAsync(string mobileNumber);

    /// <summary>
    /// Update the connection ID for a player (for reconnection scenarios)
    /// </summary>
    Task UpdateConnectionIdAsync(string mobileNumber, string newConnectionId);
}
