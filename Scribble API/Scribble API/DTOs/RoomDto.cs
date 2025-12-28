namespace Scribble_API.DTOs;

public class RoomDto
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int TotalRounds { get; set; }
    public int RoundDurationSeconds { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CustomHintsEnabled { get; set; }
    public int HintLettersCount { get; set; }
    public List<PlayerScoreDto> Players { get; set; } = new();
}
