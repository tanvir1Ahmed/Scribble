namespace Scribble.Repository.Data.Entities;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Declined,
    Blocked
}

public class Friendship
{
    public int Id { get; set; }
    public int RequesterId { get; set; } // User who sent the request
    public User? Requester { get; set; }
    public int AddresseeId { get; set; } // User who received the request
    public User? Addressee { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
