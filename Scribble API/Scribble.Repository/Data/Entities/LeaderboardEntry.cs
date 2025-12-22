namespace Scribble.Repository.Data.Entities;

public class LeaderboardEntry
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalCorrectGuesses { get; set; }
    public double BestGuessTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
