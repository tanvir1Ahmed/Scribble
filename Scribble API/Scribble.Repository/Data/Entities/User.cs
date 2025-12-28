namespace Scribble.Repository.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Online status
    public bool IsOnline { get; set; } = false;
    public string? CurrentConnectionId { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
