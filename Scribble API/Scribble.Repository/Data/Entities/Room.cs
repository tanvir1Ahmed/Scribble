namespace Scribble.Repository.Data.Entities;

public enum RoomStatus
{
    Waiting,
    Playing,
    Finished
}

public enum RoomType
{
    Public,   // Auto-matchmaking rooms
    Private   // Custom rooms created by host
}

public class Room
{
    public int Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public RoomType RoomType { get; set; } = RoomType.Public;
    
    // Host information (for private rooms)
    public int? HostPlayerId { get; set; }
    
    // Custom room settings
    public int MinPlayers { get; set; } = DefaultMinPlayers;
    public int MaxPlayers { get; set; } = DefaultMaxPlayers;
    public int CurrentRound { get; set; } = 0;
    public int TotalRounds { get; set; } = DefaultRounds;
    public int RoundDurationSeconds { get; set; } = DefaultRoundDuration;
    public int HintLettersCount { get; set; } = DefaultHintLetters; // 0 = only first letter, -1 = auto based on word length
    public bool CustomHintsEnabled { get; set; } = false;
    
    public int CurrentDrawerIndex { get; set; } = 0;
    public string? CurrentWord { get; set; }
    public string? WordOptions { get; set; } // JSON array of 3 words
    public DateTime? RoundStartTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<Player> Players { get; set; } = new();
    
    // Constraints
    public const int MinPlayersLimit = 2;
    public const int MaxPlayersLimit = 3;
    public const int MinRoundsLimit = 2;
    public const int MaxRoundsLimit = 5;
    public const int MinDurationSeconds = 20;
    public const int MaxDurationSeconds = 180;
    
    // Defaults
    public const int DefaultMinPlayers = 2;
    public const int DefaultMaxPlayers = 3;
    public const int DefaultRounds = 3;
    public const int DefaultRoundDuration = 120;
    public const int DefaultHintLetters = 1; // Show first letter only
}
