using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly ScribbleDbContext _context;

    public RoomRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<Room?> GetByIdAsync(int roomId)
    {
        return await _context.Rooms.FindAsync(roomId);
    }

    public async Task<Room?> GetWithPlayersAsync(int roomId)
    {
        return await _context.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == roomId);
    }

    public async Task<Room?> FindAvailableRoomAsync()
    {
        return await _context.Rooms
            .Include(r => r.Players)
            .Where(r => r.Status == RoomStatus.Waiting && r.Players.Count < Room.MaxPlayers)
            .OrderBy(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Room> CreateAsync(Room room)
    {
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task UpdateAsync(Room room)
    {
        _context.Rooms.Update(room);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int roomId)
    {
        return await _context.Rooms.AnyAsync(r => r.Id == roomId);
    }
}
