using Microsoft.AspNetCore.Mvc;
using Scribble.Business.Interfaces;
using Scribble_API.DTOs;

namespace Scribble_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard([FromQuery] int count = 10)
    {
        var entries = await _leaderboardService.GetTopPlayersAsync(count);
        
        var result = entries.Select((entry, index) => new LeaderboardEntryDto
        {
            Rank = index + 1,
            Username = entry.Username,
            TotalScore = entry.TotalScore,
            GamesPlayed = entry.GamesPlayed,
            GamesWon = entry.GamesWon,
            TotalCorrectGuesses = entry.TotalCorrectGuesses,
            BestGuessTime = entry.BestGuessTime
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<LeaderboardEntryDto>> GetPlayerStats(string username)
    {
        var entry = await _leaderboardService.GetPlayerStatsAsync(username);
        
        if (entry == null)
        {
            return NotFound(new { message = "Player not found" });
        }

        // Get rank
        var topPlayers = await _leaderboardService.GetTopPlayersAsync(100);
        var rank = topPlayers.FindIndex(e => e.Username.ToLower() == username.ToLower()) + 1;
        if (rank == 0) rank = topPlayers.Count + 1;

        return Ok(new LeaderboardEntryDto
        {
            Rank = rank,
            Username = entry.Username,
            TotalScore = entry.TotalScore,
            GamesPlayed = entry.GamesPlayed,
            GamesWon = entry.GamesWon,
            TotalCorrectGuesses = entry.TotalCorrectGuesses,
            BestGuessTime = entry.BestGuessTime
        });
    }
}
