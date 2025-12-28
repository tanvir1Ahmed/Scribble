namespace Scribble.Repository.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty; // For tracking player identity across sessions
    public int? UserId { get; set; } // Link to User entity
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int Score { get; set; }
    public bool IsDrawing { get; set; }
    public bool IsHost { get; set; } // True if this player created the room
    public bool HasGuessedCorrectly { get; set; }
    public DateTime? GuessTime { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
