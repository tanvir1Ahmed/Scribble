using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IFriendshipRepository
{
    Task<Friendship?> GetByIdAsync(int id);
    Task<Friendship?> GetFriendshipAsync(int userId1, int userId2);
    Task<List<Friendship>> GetFriendsAsync(int userId);
    Task<List<Friendship>> GetPendingRequestsAsync(int userId);
    Task<List<Friendship>> GetSentRequestsAsync(int userId);
    Task<Friendship> CreateAsync(Friendship friendship);
    Task UpdateAsync(Friendship friendship);
    Task DeleteAsync(Friendship friendship);
    Task<bool> AreFriendsAsync(int userId1, int userId2);
    Task<bool> HasPendingRequestAsync(int requesterId, int addresseeId);
}
