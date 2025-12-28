using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;

namespace Scribble.Business.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> ValidateTokenAsync(string token);
    Task<User?> GetUserByMobileNumberAsync(string mobileNumber);
    Task<User?> GetUserByIdAsync(int userId);
    Task<List<User>> SearchUsersAsync(string searchTerm, int currentUserId);
}
