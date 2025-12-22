namespace Scribble_API.DTOs;

public class PlayerLeftDto
{
    public string Username { get; set; } = string.Empty;
    public List<PlayerScoreDto> Players { get; set; } = new();
    public int PlayerCount { get; set; }
}
