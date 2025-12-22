namespace Scribble.Repository.Data.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsCorrectGuess { get; set; }
    public bool IsSystemMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
