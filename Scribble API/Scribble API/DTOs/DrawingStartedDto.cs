namespace Scribble_API.DTOs;

public class DrawingStartedDto
{
    public string Hint { get; set; } = string.Empty;
    public int WordLength { get; set; }
    public int Duration { get; set; }
}
