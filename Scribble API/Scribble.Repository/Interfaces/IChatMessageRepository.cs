using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.Interfaces;

public interface IChatMessageRepository
{
    Task<ChatMessage?> GetByIdAsync(int id);
    Task<List<ChatMessage>> GetByRoomIdAsync(int roomId);
    Task<ChatMessage> CreateAsync(ChatMessage chatMessage);
}
