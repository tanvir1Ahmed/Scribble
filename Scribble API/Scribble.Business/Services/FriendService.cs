using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;

namespace Scribble.Business.Services;

public class FriendService : IFriendService
{
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly IUserRepository _userRepository;

    public FriendService(
        IFriendshipRepository friendshipRepository,
        IUserRepository userRepository)
    {
        _friendshipRepository = friendshipRepository;
        _userRepository = userRepository;
    }

    public async Task<FriendRequestResult> SendFriendRequestAsync(int requesterId, int addresseeId)
    {
        // Cannot send friend request to yourself
        if (requesterId == addresseeId)
        {
            return new FriendRequestResult { Success = false, Error = "Cannot send friend request to yourself" };
        }

        // Check if addressee exists
        var addressee = await _userRepository.GetByIdAsync(addresseeId);
        if (addressee == null)
        {
            return new FriendRequestResult { Success = false, Error = "User not found" };
        }

        // Check if already friends or pending request exists
        var existingFriendship = await _friendshipRepository.GetFriendshipAsync(requesterId, addresseeId);
        if (existingFriendship != null)
        {
            if (existingFriendship.Status == FriendshipStatus.Accepted)
            {
                return new FriendRequestResult { Success = false, Error = "Already friends" };
            }
            if (existingFriendship.Status == FriendshipStatus.Pending)
            {
                return new FriendRequestResult { Success = false, Error = "Friend request already pending" };
            }
            if (existingFriendship.Status == FriendshipStatus.Blocked)
            {
                return new FriendRequestResult { Success = false, Error = "Cannot send friend request" };
            }
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = addresseeId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _friendshipRepository.CreateAsync(friendship);

        return new FriendRequestResult { Success = true, FriendshipId = friendship.Id };
    }

    public async Task<FriendRequestResult> AcceptFriendRequestAsync(int friendshipId, int userId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
            return new FriendRequestResult { Success = false, Error = "Friend request not found" };
        }

        if (friendship.AddresseeId != userId)
        {
            return new FriendRequestResult { Success = false, Error = "Not authorized to accept this request" };
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            return new FriendRequestResult { Success = false, Error = "Request is not pending" };
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = DateTime.UtcNow;
        await _friendshipRepository.UpdateAsync(friendship);

        return new FriendRequestResult { Success = true, FriendshipId = friendship.Id };
    }

    public async Task<FriendRequestResult> DeclineFriendRequestAsync(int friendshipId, int userId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
            return new FriendRequestResult { Success = false, Error = "Friend request not found" };
        }

        if (friendship.AddresseeId != userId)
        {
            return new FriendRequestResult { Success = false, Error = "Not authorized to decline this request" };
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            return new FriendRequestResult { Success = false, Error = "Request is not pending" };
        }

        friendship.Status = FriendshipStatus.Declined;
        friendship.RespondedAt = DateTime.UtcNow;
        await _friendshipRepository.UpdateAsync(friendship);

        return new FriendRequestResult { Success = true };
    }

    public async Task<FriendRequestResult> RemoveFriendAsync(int userId, int friendId)
    {
        var friendship = await _friendshipRepository.GetFriendshipAsync(userId, friendId);
        if (friendship == null || friendship.Status != FriendshipStatus.Accepted)
        {
            return new FriendRequestResult { Success = false, Error = "Friendship not found" };
        }

        await _friendshipRepository.DeleteAsync(friendship);
        return new FriendRequestResult { Success = true };
    }

    public async Task<List<FriendModel>> GetFriendsAsync(int userId)
    {
        var friendships = await _friendshipRepository.GetFriendsAsync(userId);
        var friends = new List<FriendModel>();

        foreach (var f in friendships)
        {
            var friendUser = f.RequesterId == userId ? f.Addressee : f.Requester;
            if (friendUser != null)
            {
                friends.Add(new FriendModel
                {
                    UserId = friendUser.Id,
                    Username = friendUser.Username,
                    IsOnline = friendUser.IsOnline,
                    LastSeenAt = friendUser.LastSeenAt,
                    FriendsSince = f.RespondedAt ?? f.CreatedAt
                });
            }
        }

        return friends.OrderByDescending(f => f.IsOnline).ThenBy(f => f.Username).ToList();
    }

    public async Task<List<FriendRequestModel>> GetPendingRequestsAsync(int userId)
    {
        var requests = await _friendshipRepository.GetPendingRequestsAsync(userId);
        return requests.Select(r => new FriendRequestModel
        {
            FriendshipId = r.Id,
            UserId = r.RequesterId,
            Username = r.Requester?.Username ?? "Unknown",
            IsOnline = r.Requester?.IsOnline ?? false,
            RequestedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<List<FriendRequestModel>> GetSentRequestsAsync(int userId)
    {
        var requests = await _friendshipRepository.GetSentRequestsAsync(userId);
        return requests.Select(r => new FriendRequestModel
        {
            FriendshipId = r.Id,
            UserId = r.AddresseeId,
            Username = r.Addressee?.Username ?? "Unknown",
            IsOnline = r.Addressee?.IsOnline ?? false,
            RequestedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<bool> AreFriendsAsync(int userId1, int userId2)
    {
        return await _friendshipRepository.AreFriendsAsync(userId1, userId2);
    }
}
