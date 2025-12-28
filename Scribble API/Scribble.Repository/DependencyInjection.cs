using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scribble.Repository.DbContext;
using Scribble.Repository.Interfaces;
using Scribble.Repository.Repositories;

namespace Scribble.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositoryServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ScribbleDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameScoreRepository, GameScoreRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IRoomInvitationRepository, RoomInvitationRepository>();

        return services;
    }

    public static void EnsureDatabaseCreated(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScribbleDbContext>();
        db.Database.EnsureCreated();
    }
}
