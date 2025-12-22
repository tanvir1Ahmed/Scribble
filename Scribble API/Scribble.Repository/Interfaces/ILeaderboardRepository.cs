using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface ILeaderboardRepository
{
    Task<List<LeaderboardEntry>> GetTopPlayersAsync(int count = 10);
    Task<LeaderboardEntry?> GetByUsernameAsync(string username);
    Task<LeaderboardEntry> CreateAsync(LeaderboardEntry entry);
    Task<LeaderboardEntry> UpdateAsync(LeaderboardEntry entry);
    Task<LeaderboardEntry> GetOrCreateAsync(string username);
    Task UpdateStatsAsync(string username, int scoreGained, bool won, int correctGuesses, double? bestGuessTime);
}
