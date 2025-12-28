using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace Scribble.Business.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        // Validate username
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 2 || request.Username.Length > 15)
        {
            return new AuthResult { Success = false, Error = "Name must be between 2 and 15 characters" };
        }

        // Validate mobile number
        var mobileNumber = NormalizeMobileNumber(request.MobileNumber);
        if (string.IsNullOrWhiteSpace(mobileNumber) || !IsValidMobileNumber(mobileNumber))
        {
            return new AuthResult { Success = false, Error = "Please provide a valid mobile number" };
        }

        // Check if mobile number exists
        if (await _userRepository.MobileNumberExistsAsync(mobileNumber))
        {
            return new AuthResult { Success = false, Error = "Mobile number is already registered" };
        }

        // Create user
        var user = new User
        {
            Username = request.Username.Trim(),
            MobileNumber = mobileNumber,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.CreateAsync(user);

        // Generate token
        var token = GenerateJwtToken(user);
        var expiry = DateTime.UtcNow.AddDays(30);

        return new AuthResult
        {
            Success = true,
            Token = token,
            Username = user.Username,
            MobileNumber = user.MobileNumber,
            UserId = user.Id,
            ExpiresAt = expiry
        };
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        // Validate mobile number
        var mobileNumber = NormalizeMobileNumber(request.MobileNumber);
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            return new AuthResult { Success = false, Error = "Please enter your mobile number" };
        }

        // Find user by mobile number
        var user = await _userRepository.GetByMobileNumberAsync(mobileNumber);

        if (user == null)
        {
            return new AuthResult { Success = false, Error = "Mobile number not registered. Please sign up first." };
        }

        if (!user.IsActive)
        {
            return new AuthResult { Success = false, Error = "Account is disabled" };
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Generate token
        var token = GenerateJwtToken(user);
        var expiry = DateTime.UtcNow.AddDays(30);

        return new AuthResult
        {
            Success = true,
            Token = token,
            Username = user.Username,
            MobileNumber = user.MobileNumber,
            UserId = user.Id,
            ExpiresAt = expiry
        };
    }

    public async Task<AuthResult> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "ScribbleGameSecretKey12345678901234567890");
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "ScribbleAPI",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "ScribbleApp",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return new AuthResult { Success = false, Error = "Invalid token" };
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                return new AuthResult { Success = false, Error = "User not found or inactive" };
            }

            return new AuthResult
            {
                Success = true,
                Token = token,
                Username = user.Username,
                MobileNumber = user.MobileNumber,
                UserId = user.Id
            };
        }
        catch
        {
            return new AuthResult { Success = false, Error = "Invalid or expired token" };
        }
    }

    private string GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "ScribbleGameSecretKey12345678901234567890");
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.MobilePhone, user.MobileNumber),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "ScribbleAPI",
            audience: _configuration["Jwt:Audience"] ?? "ScribbleApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string NormalizeMobileNumber(string? mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber)) return string.Empty;
        // Remove all non-digit characters except +
        return Regex.Replace(mobileNumber.Trim(), @"[^\d+]", "");
    }

    private static bool IsValidMobileNumber(string mobileNumber)
    {
        // Allow 10-15 digits, optionally starting with +
        return Regex.IsMatch(mobileNumber, @"^\+?\d{10,15}$");
    }

    public async Task<User?> GetUserByMobileNumberAsync(string mobileNumber)
    {
        var normalizedNumber = NormalizeMobileNumber(mobileNumber);
        return await _userRepository.GetByMobileNumberAsync(normalizedNumber);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm, int currentUserId)
    {
        // For now, just search by username - you can extend this
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
            return new List<User>();

        var user = await _userRepository.GetByUsernameAsync(searchTerm);
        if (user != null && user.Id != currentUserId)
        {
            return new List<User> { user };
        }
        return new List<User>();
    }
}
