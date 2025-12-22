namespace Scribble_API.DTOs;

public class GameEndedDto
{
    public List<PlayerScoreDto> Players { get; set; } = new();
}
