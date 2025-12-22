namespace Scribble.Business.Interfaces;

public interface IWordService
{
    string[] GetRandomWords(int count = 3);
    string GetRandomWord();
}
