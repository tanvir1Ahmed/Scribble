namespace Scribble_API.DTOs;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public string Username { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int TotalCorrectGuesses { get; set; }
    public double BestGuessTime { get; set; }
    public double WinRate => GamesPlayed > 0 ? Math.Round((double)GamesWon / GamesPlayed * 100, 1) : 0;
    public double AverageScore => GamesPlayed > 0 ? Math.Round((double)TotalScore / GamesPlayed, 1) : 0;
}
