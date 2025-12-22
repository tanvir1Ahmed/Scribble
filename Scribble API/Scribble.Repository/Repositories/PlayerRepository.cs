using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly ScribbleDbContext _context;

    public PlayerRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<Player?> GetByIdAsync(int playerId)
    {
        return await _context.Players.FindAsync(playerId);
    }

    public async Task<Player?> GetByConnectionIdAsync(string connectionId)
    {
        return await _context.Players
            .FirstOrDefaultAsync(p => p.ConnectionId == connectionId);
    }

    public async Task<Player?> GetByConnectionIdWithRoomAsync(string connectionId)
    {
        return await _context.Players
            .Include(p => p.Room)
            .ThenInclude(r => r!.Players)
            .FirstOrDefaultAsync(p => p.ConnectionId == connectionId);
    }

    public async Task<Player?> GetByMobileNumberAsync(string mobileNumber)
    {
        return await _context.Players
            .Include(p => p.Room)
            .FirstOrDefaultAsync(p => p.MobileNumber == mobileNumber && p.RoomId != null);
    }

    public async Task<Player?> GetByMobileNumberWithRoomAsync(string mobileNumber)
    {
        return await _context.Players
            .Include(p => p.Room)
            .ThenInclude(r => r!.Players)
            .FirstOrDefaultAsync(p => p.MobileNumber == mobileNumber && p.RoomId != null);
    }

    public async Task<List<Player>> GetByRoomIdAsync(int roomId)
    {
        return await _context.Players
            .Where(p => p.RoomId == roomId)
            .OrderByDescending(p => p.Score)
            .ToListAsync();
    }

    public async Task<Player> CreateAsync(Player player)
    {
        _context.Players.Add(player);
        await _context.SaveChangesAsync();
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        _context.Players.Update(player);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Player player)
    {
        _context.Players.Remove(player);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetPlayerCountByRoomIdAsync(int roomId)
    {
        return await _context.Players.CountAsync(p => p.RoomId == roomId);
    }

    public async Task<Player?> GetCurrentDrawerAsync(int roomId)
    {
        return await _context.Players
            .FirstOrDefaultAsync(p => p.RoomId == roomId && p.IsDrawing);
    }
}
