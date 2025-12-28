namespace Scribble_API.DTOs;

public class RoomInvitationDto
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

public class SendInvitationRequestDto
{
    public int InviteeUserId { get; set; }
}

public class InvitationResponseDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? InvitationId { get; set; }
    public int? RoomId { get; set; }
    public string? RoomCode { get; set; }
}

public class InvitationDeclinedNotificationDto
{
    public int InvitationId { get; set; }
    public int DeclinedByUserId { get; set; }
    public string DeclinedByUsername { get; set; } = string.Empty;
}
