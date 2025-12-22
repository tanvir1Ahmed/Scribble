using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class GameScoreRepository : IGameScoreRepository
{
    private readonly ScribbleDbContext _context;

    public GameScoreRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<GameScore?> GetByIdAsync(int id)
    {
        return await _context.GameScores.FindAsync(id);
    }

    public async Task<List<GameScore>> GetByRoomIdAsync(int roomId)
    {
        return await _context.GameScores
            .Where(gs => gs.RoomId == roomId)
            .OrderByDescending(gs => gs.Points)
            .ToListAsync();
    }

    public async Task<List<GameScore>> GetByPlayerIdAsync(int playerId)
    {
        return await _context.GameScores
            .Where(gs => gs.PlayerId == playerId)
            .OrderByDescending(gs => gs.CreatedAt)
            .ToListAsync();
    }

    public async Task<GameScore> CreateAsync(GameScore gameScore)
    {
        _context.GameScores.Add(gameScore);
        await _context.SaveChangesAsync();
        return gameScore;
    }
}
