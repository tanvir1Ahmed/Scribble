using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int playerId);
    Task<Player?> GetByConnectionIdAsync(string connectionId);
    Task<Player?> GetByConnectionIdWithRoomAsync(string connectionId);
    Task<Player?> GetByMobileNumberAsync(string mobileNumber);
    Task<Player?> GetByMobileNumberWithRoomAsync(string mobileNumber);
    Task<List<Player>> GetByRoomIdAsync(int roomId);
    Task<Player> CreateAsync(Player player);
    Task UpdateAsync(Player player);
    Task DeleteAsync(Player player);
    Task<int> GetPlayerCountByRoomIdAsync(int roomId);
    Task<Player?> GetCurrentDrawerAsync(int roomId);
}
