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
}
