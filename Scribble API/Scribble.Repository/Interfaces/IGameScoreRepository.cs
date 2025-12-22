using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IGameScoreRepository
{
    Task<GameScore?> GetByIdAsync(int id);
    Task<List<GameScore>> GetByRoomIdAsync(int roomId);
    Task<List<GameScore>> GetByPlayerIdAsync(int playerId);
    Task<GameScore> CreateAsync(GameScore gameScore);
}
