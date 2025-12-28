using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;

namespace Scribble.Business.Services;

public class RoomInvitationService : IRoomInvitationService
{
    private readonly IRoomInvitationRepository _invitationRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;

    public RoomInvitationService(
        IRoomInvitationRepository invitationRepository,
        IRoomRepository roomRepository,
        IUserRepository userRepository)
    {
        _invitationRepository = invitationRepository;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
    }

    public async Task<RoomInvitationResult> SendInvitationAsync(int roomId, int inviterId, int inviteeId)
    {
        // Cannot invite yourself
        if (inviterId == inviteeId)
        {
            return new RoomInvitationResult { Success = false, Error = "Cannot invite yourself" };
        }

        // Check if invitee exists
        var invitee = await _userRepository.GetByIdAsync(inviteeId);
        if (invitee == null)
        {
            return new RoomInvitationResult { Success = false, Error = "User not found" };
        }

        // Check if invitee is online
        if (!invitee.IsOnline)
        {
            return new RoomInvitationResult { Success = false, Error = "User is not online" };
        }

        // Check if room exists and is valid
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null)
        {
            return new RoomInvitationResult { Success = false, Error = "Room not found" };
        }

        if (room.Status == RoomStatus.Finished)
        {
            return new RoomInvitationResult { Success = false, Error = "Room is no longer available" };
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            return new RoomInvitationResult { Success = false, Error = "Room is full" };
        }

        // Check if there's already a pending invitation
        var existingInvitation = await _invitationRepository.GetPendingInvitationAsync(roomId, inviteeId);
        if (existingInvitation != null)
        {
            return new RoomInvitationResult { Success = false, Error = "Invitation already sent" };
        }

        var invitation = new RoomInvitation
        {
            RoomId = roomId,
            InviterId = inviterId,
            InviteeId = inviteeId,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(RoomInvitation.DefaultExpirySeconds)
        };

        await _invitationRepository.CreateAsync(invitation);

        return new RoomInvitationResult 
        { 
            Success = true, 
            InvitationId = invitation.Id,
            RoomId = roomId,
            RoomCode = room.RoomCode
        };
    }

    public async Task<RoomInvitationResult> AcceptInvitationAsync(int invitationId, int userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
        {
            return new RoomInvitationResult { Success = false, Error = "Invitation not found" };
        }

        if (invitation.InviteeId != userId)
        {
            return new RoomInvitationResult { Success = false, Error = "Not authorized" };
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return new RoomInvitationResult { Success = false, Error = "Invitation is no longer pending" };
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            return new RoomInvitationResult { Success = false, Error = "Invitation has expired" };
        }

        // Check if room is still valid
        var room = await _roomRepository.GetWithPlayersAsync(invitation.RoomId);
        if (room == null || room.Status == RoomStatus.Finished)
        {
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            return new RoomInvitationResult { Success = false, Error = "Room is no longer available" };
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            invitation.Status = InvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            return new RoomInvitationResult { Success = false, Error = "Room is full" };
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = DateTime.UtcNow;
        await _invitationRepository.UpdateAsync(invitation);

        return new RoomInvitationResult 
        { 
            Success = true, 
            InvitationId = invitation.Id,
            RoomId = room.Id,
            RoomCode = room.RoomCode
        };
    }

    public async Task<RoomInvitationResult> DeclineInvitationAsync(int invitationId, int userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
        {
            return new RoomInvitationResult { Success = false, Error = "Invitation not found" };
        }

        if (invitation.InviteeId != userId)
        {
            return new RoomInvitationResult { Success = false, Error = "Not authorized" };
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return new RoomInvitationResult { Success = false, Error = "Invitation is no longer pending" };
        }

        invitation.Status = InvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await _invitationRepository.UpdateAsync(invitation);

        return new RoomInvitationResult 
        { 
            Success = true,
            InvitationId = invitation.Id
        };
    }

    public async Task<List<RoomInvitationModel>> GetPendingInvitationsAsync(int userId)
    {
        var invitations = await _invitationRepository.GetPendingInvitationsForUserAsync(userId);
        return invitations.Select(i => new RoomInvitationModel
        {
            InvitationId = i.Id,
            RoomId = i.RoomId,
            RoomCode = i.Room?.RoomCode ?? string.Empty,
            InviterId = i.InviterId,
            InviterName = i.Inviter?.Username ?? "Unknown",
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            SecondsRemaining = (int)Math.Max(0, (i.ExpiresAt - DateTime.UtcNow).TotalSeconds)
        }).ToList();
    }

    public async Task ExpireOldInvitationsAsync()
    {
        await _invitationRepository.ExpireOldInvitationsAsync();
    }
}
