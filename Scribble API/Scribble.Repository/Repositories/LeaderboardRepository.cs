using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;

namespace Scribble.Repository.Repositories;

public class LeaderboardRepository : ILeaderboardRepository
{
    private readonly ScribbleDbContext _context;

    public LeaderboardRepository(ScribbleDbContext context)
    {
        _context = context;
    }

    public async Task<List<LeaderboardEntry>> GetTopPlayersAsync(int count = 10)
    {
        
        return await _context.LeaderboardEntries
            .OrderByDescending(e => e.TotalScore)
            .ThenByDescending(e => e.GamesWon)
            .ThenBy(e => e.BestGuessTime)
            .Take(count)
            .ToListAsync();
    }

    public async Task<LeaderboardEntry?> GetByUsernameAsync(string username)
    {
        return await _context.LeaderboardEntries
            .FirstOrDefaultAsync(e => e.Username.ToLower() == username.ToLower());
    }

    public async Task<LeaderboardEntry> CreateAsync(LeaderboardEntry entry)
    {
        _context.LeaderboardEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<LeaderboardEntry> UpdateAsync(LeaderboardEntry entry)
    {
        entry.UpdatedAt = DateTime.UtcNow;
        _context.LeaderboardEntries.Update(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<LeaderboardEntry> GetOrCreateAsync(string username)
    {
        var entry = await GetByUsernameAsync(username);
        if (entry == null)
        {
            entry = new LeaderboardEntry
            {
                Username = username,
                TotalScore = 0,
                GamesPlayed = 0,
                GamesWon = 0,
                TotalCorrectGuesses = 0,
                BestGuessTime = 0
            };
            await CreateAsync(entry);
        }
        return entry;
    }

    public async Task UpdateStatsAsync(string username, int scoreGained, bool won, int correctGuesses, double? bestGuessTime)
    {
        var entry = await GetOrCreateAsync(username);
        
        entry.TotalScore += scoreGained;
        entry.GamesPlayed += 1;
        entry.TotalCorrectGuesses += correctGuesses;
        
        if (won)
        {
            entry.GamesWon += 1;
        }
        
        if (bestGuessTime.HasValue && bestGuessTime.Value > 0)
        {
            if (entry.BestGuessTime == 0 || bestGuessTime.Value < entry.BestGuessTime)
            {
                entry.BestGuessTime = bestGuessTime.Value;
            }
        }
        
        await UpdateAsync(entry);
    }
}
