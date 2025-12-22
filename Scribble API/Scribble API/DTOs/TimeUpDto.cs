namespace Scribble_API.DTOs;

public class TimeUpDto
{
    public string? CorrectWord { get; set; }
    public List<PlayerScoreDto> Players { get; set; } = new();
    public bool AllGuessed { get; set; }
}
