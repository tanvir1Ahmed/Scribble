namespace Scribble.Repository.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty; // For tracking player identity across sessions
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int Score { get; set; }
    public bool IsDrawing { get; set; }
    public bool HasGuessedCorrectly { get; set; }
    public DateTime? GuessTime { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
