using Microsoft.AspNetCore.Mvc;
using Scribble.Business.Interfaces;
using Scribble_API.DTOs;

namespace Scribble_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;

    public GameController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("room/{roomId}")]
    public async Task<ActionResult<RoomDto>> GetRoom(int roomId)
    {
        var room = await _gameService.GetRoomWithPlayersAsync(roomId);
        if (room == null)
            return NotFound();

        var scores = await _gameService.GetScoresAsync(roomId);

        return Ok(new RoomDto
        {
            RoomId = room.Id,
            RoomCode = room.RoomCode,
            PlayerCount = room.Players.Count,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToString(),
            Players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList()
        });
    }

    [HttpGet("room/{roomId}/scores")]
    public async Task<ActionResult<List<PlayerScoreDto>>> GetScores(int roomId)
    {
        var scores = await _gameService.GetScoresAsync(roomId);

        return Ok(scores.Select(s => new PlayerScoreDto
        {
            PlayerId = s.PlayerId,
            Username = s.Username,
            Score = s.Score,
            IsDrawing = s.IsDrawing,
            HasGuessedCorrectly = s.HasGuessedCorrectly
        }).ToList());
    }
}
