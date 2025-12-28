using Microsoft.Extensions.DependencyInjection;
using Scribble.Business.Interfaces;
using Scribble.Business.Services;

namespace Scribble.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddSingleton<IWordService, WordService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPlayerRoomCacheService, PlayerRoomCacheService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IRoomInvitationService, RoomInvitationService>();

        return services;
    }
}
