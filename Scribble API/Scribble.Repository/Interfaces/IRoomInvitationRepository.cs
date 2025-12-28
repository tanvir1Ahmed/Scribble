using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IRoomInvitationRepository
{
    Task<RoomInvitation?> GetByIdAsync(int id);
    Task<RoomInvitation?> GetPendingInvitationAsync(int roomId, int inviteeId);
    Task<List<RoomInvitation>> GetPendingInvitationsForUserAsync(int userId);
    Task<List<RoomInvitation>> GetInvitationsByRoomAsync(int roomId);
    Task<RoomInvitation> CreateAsync(RoomInvitation invitation);
    Task UpdateAsync(RoomInvitation invitation);
    Task DeleteAsync(RoomInvitation invitation);
    Task ExpireOldInvitationsAsync();
}
