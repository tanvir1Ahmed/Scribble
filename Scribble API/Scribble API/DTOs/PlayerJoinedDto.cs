namespace Scribble_API.DTOs;

public class PlayerJoinedDto
{
    public int PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
}
