using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scribble.Business.Interfaces;
using Scribble_API.DTOs;
using System.Security.Claims;

namespace Scribble_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendService _friendService;
    private readonly IAuthService _authService;

    public FriendsController(IFriendService friendService, IAuthService authService)
    {
        _friendService = friendService;
        _authService = authService;
    }

    private int? GetCurrentUserId()
    {
        var mobileNumber = User.FindFirst(ClaimTypes.MobilePhone)?.Value 
            ?? User.FindFirst("mobile_number")?.Value;
        
        if (string.IsNullOrEmpty(mobileNumber)) return null;
        
        // This is synchronous but we need to get user ID
        var user = _authService.GetUserByMobileNumberAsync(mobileNumber).Result;
        return user?.Id;
    }

    [HttpGet]
    public async Task<ActionResult<FriendsListDto>> GetFriends()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var friends = await _friendService.GetFriendsAsync(userId.Value);
        var pendingRequests = await _friendService.GetPendingRequestsAsync(userId.Value);
        var sentRequests = await _friendService.GetSentRequestsAsync(userId.Value);

        return Ok(new FriendsListDto
        {
            Friends = friends.Select(f => new FriendDto
            {
                UserId = f.UserId,
                Username = f.Username,
                IsOnline = f.IsOnline,
                LastSeenAt = f.LastSeenAt,
                FriendsSince = f.FriendsSince
            }).ToList(),
            PendingRequests = pendingRequests.Select(r => new FriendRequestDto
            {
                FriendshipId = r.FriendshipId,
                UserId = r.UserId,
                Username = r.Username,
                IsOnline = r.IsOnline,
                RequestedAt = r.RequestedAt
            }).ToList(),
            SentRequests = sentRequests.Select(r => new FriendRequestDto
            {
                FriendshipId = r.FriendshipId,
                UserId = r.UserId,
                Username = r.Username,
                IsOnline = r.IsOnline,
                RequestedAt = r.RequestedAt
            }).ToList()
        });
    }

    [HttpPost("request")]
    public async Task<ActionResult<FriendRequestResponseDto>> SendFriendRequest([FromBody] SendFriendRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _friendService.SendFriendRequestAsync(userId.Value, request.UserId);

        return Ok(new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            FriendshipId = result.FriendshipId
        });
    }

    [HttpPost("accept/{friendshipId}")]
    public async Task<ActionResult<FriendRequestResponseDto>> AcceptFriendRequest(int friendshipId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _friendService.AcceptFriendRequestAsync(friendshipId, userId.Value);

        return Ok(new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            FriendshipId = result.FriendshipId
        });
    }

    [HttpPost("decline/{friendshipId}")]
    public async Task<ActionResult<FriendRequestResponseDto>> DeclineFriendRequest(int friendshipId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _friendService.DeclineFriendRequestAsync(friendshipId, userId.Value);

        return Ok(new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error
        });
    }

    [HttpDelete("{friendId}")]
    public async Task<ActionResult<FriendRequestResponseDto>> RemoveFriend(int friendId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _friendService.RemoveFriendAsync(userId.Value, friendId);

        return Ok(new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error
        });
    }
}
