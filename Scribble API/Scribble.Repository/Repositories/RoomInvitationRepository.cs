using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class RoomInvitationRepository : IRoomInvitationRepository
{
    private readonly ScribbleDbContext _context;

    public RoomInvitationRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<RoomInvitation?> GetByIdAsync(int id)
    {
        return await _context.RoomInvitations
            .Include(i => i.Room)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<RoomInvitation?> GetPendingInvitationAsync(int roomId, int inviteeId)
    {
        return await _context.RoomInvitations
            .Include(i => i.Room)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .FirstOrDefaultAsync(i => 
                i.RoomId == roomId && 
                i.InviteeId == inviteeId && 
                i.Status == InvitationStatus.Pending &&
                i.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<List<RoomInvitation>> GetPendingInvitationsForUserAsync(int userId)
    {
        return await _context.RoomInvitations
            .Include(i => i.Room)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => 
                i.InviteeId == userId && 
                i.Status == InvitationStatus.Pending &&
                i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<RoomInvitation>> GetInvitationsByRoomAsync(int roomId)
    {
        return await _context.RoomInvitations
            .Include(i => i.Room)
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => i.RoomId == roomId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<RoomInvitation> CreateAsync(RoomInvitation invitation)
    {
        _context.RoomInvitations.Add(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task UpdateAsync(RoomInvitation invitation)
    {
        _context.RoomInvitations.Update(invitation);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(RoomInvitation invitation)
    {
        _context.RoomInvitations.Remove(invitation);
        await _context.SaveChangesAsync();
    }

    public async Task ExpireOldInvitationsAsync()
    {
        var expiredInvitations = await _context.RoomInvitations
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var invitation in expiredInvitations)
        {
            invitation.Status = InvitationStatus.Expired;
        }

        await _context.SaveChangesAsync();
    }
}
