namespace Scribble_API.DTOs;

public class ChatMessageDto
{
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public bool IsSystem { get; set; }
}
