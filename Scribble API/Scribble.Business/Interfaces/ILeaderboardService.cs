using Scribble.Repository.Data.Entities;

namespace Scribble.Business.Interfaces;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntry>> GetTopPlayersAsync(int count = 10);
    Task<LeaderboardEntry?> GetPlayerStatsAsync(string username);
    Task UpdatePlayerStatsAsync(string username, int scoreGained, bool won, int correctGuesses, double? bestGuessTime);
    Task RecordGameEndAsync(int roomId);
}
