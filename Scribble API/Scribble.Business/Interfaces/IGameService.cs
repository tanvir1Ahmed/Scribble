using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;

namespace Scribble.Business.Interfaces;

public interface IGameService
{
    // Existing methods
    Task<Room> FindOrCreateRoomAsync();
    Task<Player> JoinRoomAsync(string connectionId, string username, string mobileNumber, int roomId, int? userId = null, bool isHost = false);
    Task<Player?> GetPlayerByConnectionIdAsync(string connectionId);
    Task<Player?> GetPlayerByMobileNumberAsync(string mobileNumber);
    Task<Room?> GetRoomByIdAsync(int roomId);
    Task<Room?> GetRoomWithPlayersAsync(int roomId);
    Task RemovePlayerAsync(string connectionId);
    Task RemovePlayerByMobileNumberAsync(string mobileNumber);
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
    
    // New methods for custom rooms
    Task<CreateRoomResult> CreateCustomRoomAsync(string connectionId, string username, string mobileNumber, int? userId, CreateRoomSettings settings);
    Task<JoinRoomResult> JoinRoomByCodeAsync(string connectionId, string username, string mobileNumber, int? userId, string roomCode);
    Task<Room?> GetRoomByCodeAsync(string roomCode);
    Task<bool> RemovePlayerFromRoomAsync(int roomId, int playerId, int hostPlayerId);
    Task<bool> RestartGameAsync(int roomId, int hostPlayerId);
    Task<string> GenerateHintAsync(string word, int hintLettersCount, bool customHintsEnabled);
    Task<bool> IsPlayerHostAsync(int roomId, int playerId);
    Task<bool> CanStartGameAsync(int roomId);
    Task<(bool CanStart, string? Error)> CanStartGameWithReasonAsync(int roomId);
}
