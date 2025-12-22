namespace Scribble.Business.Models;

public class GuessResult
{
    public bool IsCorrect { get; set; }
    public int Points { get; set; }
    public double TimeTaken { get; set; }
}
