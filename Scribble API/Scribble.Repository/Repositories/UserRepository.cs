using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ScribbleDbContext _context;

    public UserRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User?> GetByMobileNumberAsync(string mobileNumber)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.MobileNumber == mobileNumber);
    }

    public async Task<bool> MobileNumberExistsAsync(string mobileNumber)
    {
        return await _context.Users
            .AnyAsync(u => u.MobileNumber == mobileNumber);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task SetOnlineStatusAsync(int userId, bool isOnline, string? connectionId = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsOnline = isOnline;
            user.CurrentConnectionId = connectionId;
            user.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<User>> GetOnlineUsersAsync(IEnumerable<int> userIds)
    {
        return await _context.Users
            .Where(u => userIds.Contains(u.Id) && u.IsOnline)
            .ToListAsync();
    }

    public async Task<User?> GetByConnectionIdAsync(string connectionId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.CurrentConnectionId == connectionId);
    }
}
