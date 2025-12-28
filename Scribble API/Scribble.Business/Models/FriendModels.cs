namespace Scribble.Business.Models;

public class FriendModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime FriendsSince { get; set; }
}

public class FriendRequestModel
{
    public int FriendshipId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime RequestedAt { get; set; }
}

public class FriendRequestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? FriendshipId { get; set; }
}
