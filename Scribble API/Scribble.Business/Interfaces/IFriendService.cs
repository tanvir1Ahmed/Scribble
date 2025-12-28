using Scribble.Business.Models;

namespace Scribble.Business.Interfaces;

public interface IFriendService
{
    Task<FriendRequestResult> SendFriendRequestAsync(int requesterId, int addresseeId);
    Task<FriendRequestResult> AcceptFriendRequestAsync(int friendshipId, int userId);
    Task<FriendRequestResult> DeclineFriendRequestAsync(int friendshipId, int userId);
    Task<FriendRequestResult> RemoveFriendAsync(int userId, int friendId);
    Task<List<FriendModel>> GetFriendsAsync(int userId);
    Task<List<FriendRequestModel>> GetPendingRequestsAsync(int userId);
    Task<List<FriendRequestModel>> GetSentRequestsAsync(int userId);
    Task<bool> AreFriendsAsync(int userId1, int userId2);
}
