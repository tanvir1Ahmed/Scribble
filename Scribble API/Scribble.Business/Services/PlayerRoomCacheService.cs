using Microsoft.Extensions.Caching.Distributed;
using Scribble.Business.Interfaces;
using System.Text.Json;

namespace Scribble.Business.Services;

/// <summary>
/// Implementation of player-room cache using distributed cache (Redis or in-memory)
/// </summary>
public class PlayerRoomCacheService : IPlayerRoomCacheService
{
    private readonly IDistributedCache _cache;
    private const string CacheKeyPrefix = "player_room:";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24); // Auto-expire after 24 hours

    public PlayerRoomCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string GetCacheKey(string mobileNumber) => $"{CacheKeyPrefix}{mobileNumber}";

    public async Task SetPlayerRoomAsync(string mobileNumber, PlayerRoomInfo roomInfo)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiry
        };

        var json = JsonSerializer.Serialize(roomInfo);
        await _cache.SetStringAsync(GetCacheKey(mobileNumber), json, options);
    }

    public async Task<PlayerRoomInfo?> GetPlayerRoomAsync(string mobileNumber)
    {
        var json = await _cache.GetStringAsync(GetCacheKey(mobileNumber));
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<PlayerRoomInfo>(json);
    }

    public async Task RemovePlayerRoomAsync(string mobileNumber)
    {
        await _cache.RemoveAsync(GetCacheKey(mobileNumber));
    }

    public async Task<bool> IsPlayerInRoomAsync(string mobileNumber)
    {
        var json = await _cache.GetStringAsync(GetCacheKey(mobileNumber));
        return !string.IsNullOrEmpty(json);
    }

    public async Task UpdateConnectionIdAsync(string mobileNumber, string newConnectionId)
    {
        var roomInfo = await GetPlayerRoomAsync(mobileNumber);
        if (roomInfo != null)
        {
            roomInfo.ConnectionId = newConnectionId;
            await SetPlayerRoomAsync(mobileNumber, roomInfo);
        }
    }
}
