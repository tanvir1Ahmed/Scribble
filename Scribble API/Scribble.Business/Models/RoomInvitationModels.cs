namespace Scribble.Business.Models;

public class RoomInvitationModel
{
    public int InvitationId { get; set; }
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public int InviterId { get; set; }
    public string InviterName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int SecondsRemaining { get; set; }
}

public class RoomInvitationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? InvitationId { get; set; }
    public int? RoomId { get; set; }
    public string? RoomCode { get; set; }
}
