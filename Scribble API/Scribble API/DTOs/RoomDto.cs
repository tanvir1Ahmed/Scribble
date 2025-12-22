namespace Scribble_API.DTOs;

public class RoomDto
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PlayerScoreDto> Players { get; set; } = new();
}
