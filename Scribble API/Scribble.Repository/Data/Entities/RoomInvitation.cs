namespace Scribble.Repository.Data.Entities;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Expired
}

public class RoomInvitation
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public int InviterId { get; set; } // User who sent the invitation (usually host)
    public User? Inviter { get; set; }
    public int InviteeId { get; set; } // User who received the invitation
    public User? Invitee { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddSeconds(DefaultExpirySeconds);
    public DateTime? RespondedAt { get; set; }
    
    public const int DefaultExpirySeconds = 30; // 30 seconds to respond
}
