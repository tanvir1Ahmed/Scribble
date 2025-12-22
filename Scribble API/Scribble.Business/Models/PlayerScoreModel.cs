namespace Scribble.Business.Models;

public class PlayerScoreModel
{
    public int PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsDrawing { get; set; }
    public bool HasGuessedCorrectly { get; set; }
}
