using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ScribbleDbContext _context;

    public ChatMessageRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<ChatMessage?> GetByIdAsync(int id)
    {
        return await _context.ChatMessages.FindAsync(id);
    }

    public async Task<List<ChatMessage>> GetByRoomIdAsync(int roomId)
    {
        return await _context.ChatMessages
            .Where(cm => cm.RoomId == roomId)
            .OrderBy(cm => cm.SentAt)
            .ToListAsync();
    }

    public async Task<ChatMessage> CreateAsync(ChatMessage chatMessage)
    {
        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();
        return chatMessage;
    }
}
