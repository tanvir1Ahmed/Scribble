using Scribble.Business.Interfaces;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;

namespace Scribble.Business.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly ILeaderboardRepository _leaderboardRepository;
    private readonly IRoomRepository _roomRepository;

    public LeaderboardService(
        ILeaderboardRepository leaderboardRepository,
        IRoomRepository roomRepository)
    {
        _leaderboardRepository = leaderboardRepository;
        _roomRepository = roomRepository;
    }

    public async Task<List<LeaderboardEntry>> GetTopPlayersAsync(int count = 10)
    {
        return await _leaderboardRepository.GetTopPlayersAsync(count);
    }

    public async Task<LeaderboardEntry?> GetPlayerStatsAsync(string username)
    {
        return await _leaderboardRepository.GetByUsernameAsync(username);
    }

    public async Task UpdatePlayerStatsAsync(string username, int scoreGained, bool won, int correctGuesses, double? bestGuessTime)
    {
        await _leaderboardRepository.UpdateStatsAsync(username, scoreGained, won, correctGuesses, bestGuessTime);
    }

    public async Task RecordGameEndAsync(int roomId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return;

        var players = room.Players.OrderByDescending(p => p.Score).ToList();
        if (players.Count == 0) return;

        var winnerScore = players[0].Score;

        foreach (var player in players)
        {
            var isWinner = player.Score == winnerScore && winnerScore > 0;
            var correctGuesses = player.HasGuessedCorrectly ? 1 : 0;
            double? bestTime = player.GuessTime.HasValue 
                ? (DateTime.UtcNow - player.GuessTime.Value).TotalSeconds 
                : null;

            await _leaderboardRepository.UpdateStatsAsync(
                player.Username,
                player.Score,
                isWinner,
                correctGuesses,
                bestTime
            );
        }
    }
}
