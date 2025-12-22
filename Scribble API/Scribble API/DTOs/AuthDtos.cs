namespace Scribble_API.DTOs;

public class RegisterRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    public string MobileNumber { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? MobileNumber { get; set; }
    public int UserId { get; set; }
    public string? Error { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
