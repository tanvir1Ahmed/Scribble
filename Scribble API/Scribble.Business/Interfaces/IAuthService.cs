using Scribble.Business.Models;

namespace Scribble.Business.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> ValidateTokenAsync(string token);
}
