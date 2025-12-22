using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;

namespace Scribble.Business.Interfaces;

public interface IGameService
{
    Task<Room> FindOrCreateRoomAsync();
    Task<Player> JoinRoomAsync(string connectionId, string username, string mobileNumber, int roomId);
    Task<Player?> GetPlayerByConnectionIdAsync(string connectionId);
    Task<Player?> GetPlayerByMobileNumberAsync(string mobileNumber);
    Task<Room?> GetRoomByIdAsync(int roomId);
    Task<Room?> GetRoomWithPlayersAsync(int roomId);
    Task RemovePlayerAsync(string connectionId);
    Task<bool> StartGameAsync(int roomId);
    Task<string[]> GetWordOptionsAsync(int roomId);
    Task SelectWordAsync(int roomId, string word);
    Task<GuessResult> CheckGuessAsync(int roomId, int playerId, string guess);
    Task<bool> NextTurnAsync(int roomId);
    Task<List<PlayerScoreModel>> GetScoresAsync(int roomId);
    Task ResetPlayerGuessStatusAsync(int roomId);
    Task<Player?> GetCurrentDrawerAsync(int roomId);
    Task AwardDrawerPointsAsync(int roomId, int guesserPoints, double timeTaken);
    Task<bool> IsPlayerInAnyRoomAsync(string mobileNumber);
    Task<Player?> UpdatePlayerConnectionAsync(string mobileNumber, string newConnectionId);
}
