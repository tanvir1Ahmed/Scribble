namespace Scribble_API.DTOs;

public class NewTurnDto
{
    public int CurrentRound { get; set; }
    public int TotalRounds { get; set; }
    public int? DrawerId { get; set; }
    public string? DrawerName { get; set; }
    public List<PlayerScoreDto> Players { get; set; } = new();
}
