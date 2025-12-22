using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scribble.Business.Interfaces;
using System.Security.Claims;

namespace Scribble_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly IPlayerRoomCacheService _playerRoomCache;
    private readonly IGameService _gameService;

    public RoomController(IPlayerRoomCacheService playerRoomCache, IGameService gameService)
    {
        _playerRoomCache = playerRoomCache;
        _gameService = gameService;
    }

    /// <summary>
    /// Check if the current user is already in a room
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetRoomStatus()
    {
        var mobileNumber = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        
        if (string.IsNullOrEmpty(mobileNumber))
        {
            return Ok(new { inRoom = false });
        }

        var roomInfo = await _playerRoomCache.GetPlayerRoomAsync(mobileNumber);
        
        if (roomInfo != null)
        {
            // Verify room still exists and is active
            var room = await _gameService.GetRoomWithPlayersAsync(roomInfo.RoomId);
            if (room != null && room.Status.ToString() != "Finished")
            {
                return Ok(new 
                { 
                    inRoom = true,
                    roomId = roomInfo.RoomId,
                    roomCode = roomInfo.RoomCode,
                    username = roomInfo.Username,
                    status = room.Status.ToString()
                });
            }
            else
            {
                // Room no longer valid, clear cache
                await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
            }
        }

        return Ok(new { inRoom = false });
    }

    /// <summary>
    /// Clear room assignment for current user (force leave)
    /// </summary>
    [HttpPost("leave")]
    [Authorize]
    public async Task<IActionResult> LeaveRoom()
    {
        var mobileNumber = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        
        if (string.IsNullOrEmpty(mobileNumber))
        {
            return BadRequest(new { error = "Mobile number not found in token" });
        }

        // Get player from database
        var player = await _gameService.GetPlayerByMobileNumberAsync(mobileNumber);
        
        if (player != null && player.RoomId != null)
        {
            // Remove player from the room
            await _gameService.RemovePlayerAsync(player.ConnectionId);
        }

        // Clear cache
        await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);

        return Ok(new { success = true, message = "Left room successfully" });
    }
}
