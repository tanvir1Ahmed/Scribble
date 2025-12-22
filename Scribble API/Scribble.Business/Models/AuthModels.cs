namespace Scribble.Business.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? MobileNumber { get; set; }
    public int UserId { get; set; }
    public string? Error { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string MobileNumber { get; set; } = string.Empty;
}
