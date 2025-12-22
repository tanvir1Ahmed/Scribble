namespace Scribble.Repository.Data.Entities;

public class GameScore
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public int Round { get; set; }
    public int Points { get; set; }
    public string? GuessedWord { get; set; }
    public double? TimeTaken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
