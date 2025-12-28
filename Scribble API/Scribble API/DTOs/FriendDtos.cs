namespace Scribble_API.DTOs;

public class FriendDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime FriendsSince { get; set; }
}

public class FriendRequestDto
{
    public int FriendshipId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime RequestedAt { get; set; }
}

public class SendFriendRequestDto
{
    public int UserId { get; set; }
}

public class FriendRequestResponseDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? FriendshipId { get; set; }
}

public class FriendsListDto
{
    public List<FriendDto> Friends { get; set; } = new();
    public List<FriendRequestDto> PendingRequests { get; set; } = new();
    public List<FriendRequestDto> SentRequests { get; set; } = new();
}
