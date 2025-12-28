using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class FriendshipRepository : IFriendshipRepository
{
    private readonly ScribbleDbContext _context;

    public FriendshipRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<Friendship?> GetByIdAsync(int id)
    {
        return await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Friendship?> GetFriendshipAsync(int userId1, int userId2)
    {
        return await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .FirstOrDefaultAsync(f =>
                (f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                (f.RequesterId == userId2 && f.AddresseeId == userId1));
    }

    public async Task<List<Friendship>> GetFriendsAsync(int userId)
    {
        return await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => 
                (f.RequesterId == userId || f.AddresseeId == userId) && 
                f.Status == FriendshipStatus.Accepted)
            .ToListAsync();
    }

    public async Task<List<Friendship>> GetPendingRequestsAsync(int userId)
    {
        return await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Friendship>> GetSentRequestsAsync(int userId)
    {
        return await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.RequesterId == userId && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<Friendship> CreateAsync(Friendship friendship)
    {
        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();
        return friendship;
    }

    public async Task UpdateAsync(Friendship friendship)
    {
        _context.Friendships.Update(friendship);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Friendship friendship)
    {
        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> AreFriendsAsync(int userId1, int userId2)
    {
        return await _context.Friendships
            .AnyAsync(f =>
                ((f.RequesterId == userId1 && f.AddresseeId == userId2) ||
                 (f.RequesterId == userId2 && f.AddresseeId == userId1)) &&
                f.Status == FriendshipStatus.Accepted);
    }

    public async Task<bool> HasPendingRequestAsync(int requesterId, int addresseeId)
    {
        return await _context.Friendships
            .AnyAsync(f =>
                f.RequesterId == requesterId && 
                f.AddresseeId == addresseeId && 
                f.Status == FriendshipStatus.Pending);
    }
}
