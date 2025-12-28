using Scribble.Business.Models;

namespace Scribble.Business.Interfaces;

public interface IRoomInvitationService
{
    Task<RoomInvitationResult> SendInvitationAsync(int roomId, int inviterId, int inviteeId);
    Task<RoomInvitationResult> AcceptInvitationAsync(int invitationId, int userId);
    Task<RoomInvitationResult> DeclineInvitationAsync(int invitationId, int userId);
    Task<List<RoomInvitationModel>> GetPendingInvitationsAsync(int userId);
    Task ExpireOldInvitationsAsync();
}
