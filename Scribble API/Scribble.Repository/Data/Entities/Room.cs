namespace Scribble.Repository.Data.Entities;

public enum RoomStatus
{
    Waiting,
    Playing,
    Finished
}

public class Room
{
    public int Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public int CurrentRound { get; set; } = 0;
    public int TotalRounds { get; set; } = 3;
    public int CurrentDrawerIndex { get; set; } = 0;
    public string? CurrentWord { get; set; }
    public string? WordOptions { get; set; } // JSON array of 3 words
    public DateTime? RoundStartTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<Player> Players { get; set; } = new();
    
    public const int MaxPlayers = 3;
    public const int RoundDurationSeconds = 20;
}
