using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByMobileNumberAsync(string mobileNumber);
    Task<bool> MobileNumberExistsAsync(string mobileNumber);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task SetOnlineStatusAsync(int userId, bool isOnline, string? connectionId = null);
    Task<List<User>> GetOnlineUsersAsync(IEnumerable<int> userIds);
    Task<User?> GetByConnectionIdAsync(string connectionId);
}
