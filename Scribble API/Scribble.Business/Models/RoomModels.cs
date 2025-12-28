namespace Scribble.Business.Models;

public class CreateRoomSettings
{
    public int MaxPlayers { get; set; } = 3;
    public int TotalRounds { get; set; } = 3;
    public int RoundDurationSeconds { get; set; } = 120;
    public int HintLettersCount { get; set; } = 1;
    public bool CustomHintsEnabled { get; set; } = false;
}

public class CreateRoomResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? RoomId { get; set; }
    public string? RoomCode { get; set; }
}

public class JoinRoomResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? RoomId { get; set; }
    public string? RoomCode { get; set; }
    public int? PlayerId { get; set; }
    public bool IsHost { get; set; }
}
