using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(int roomId);
    Task<Room?> GetByRoomCodeAsync(string roomCode);
    Task<Room?> GetWithPlayersAsync(int roomId);
    Task<Room?> GetWithPlayersNoTrackingAsync(int roomId);
    Task<Room?> GetWithPlayersByCodeAsync(string roomCode);
    Task<Room?> FindAvailableRoomAsync();
    Task<Room> CreateAsync(Room room);
    Task UpdateAsync(Room room);
    Task<bool> ExistsAsync(int roomId);
    Task<bool> RoomCodeExistsAsync(string roomCode);
}
