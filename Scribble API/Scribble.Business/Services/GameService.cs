using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;
using System.Text.Json;

namespace Scribble.Business.Services;

public class GameService : IGameService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameScoreRepository _gameScoreRepository;
    private readonly IWordService _wordService;
    private static readonly SemaphoreSlim _roomLock = new(1, 1);

    public GameService(
        IRoomRepository roomRepository,
        IPlayerRepository playerRepository,
        IGameScoreRepository gameScoreRepository,
        IWordService wordService)
    {
        _roomRepository = roomRepository;
        _playerRepository = playerRepository;
        _gameScoreRepository = gameScoreRepository;
        _wordService = wordService;
    }

    public async Task<Room> FindOrCreateRoomAsync()
    {
        await _roomLock.WaitAsync();
        try
        {
            var room = await _roomRepository.FindAvailableRoomAsync();

            if (room == null)
            {
                room = new Room
                {
                    RoomCode = GenerateRoomCode(),
                    Status = RoomStatus.Waiting,
                    CreatedAt = DateTime.UtcNow
                };
                room = await _roomRepository.CreateAsync(room);
            }

            return room;
        }
        finally
        {
            _roomLock.Release();
        }
    }

    public async Task<Player> JoinRoomAsync(string connectionId, string username, string mobileNumber, int roomId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);

        if (room == null)
            throw new Exception("Room not found");

        if (room.Players.Count >= Room.MaxPlayers)
            throw new Exception("Room is full");

        // Check if this player is already in the room (by mobile number)
        var existingPlayer = room.Players.FirstOrDefault(p => p.MobileNumber == mobileNumber);
        if (existingPlayer != null)
        {
            // Update connection ID for reconnection
            existingPlayer.ConnectionId = connectionId;
            await _playerRepository.UpdateAsync(existingPlayer);
            return existingPlayer;
        }

        var player = new Player
        {
            ConnectionId = connectionId,
            Username = username,
            MobileNumber = mobileNumber,
            RoomId = roomId,
            Score = 0,
            IsDrawing = false,
            HasGuessedCorrectly = false,
            JoinedAt = DateTime.UtcNow
        };

        return await _playerRepository.CreateAsync(player);
    }

    public async Task<Player?> GetPlayerByConnectionIdAsync(string connectionId)
    {
        return await _playerRepository.GetByConnectionIdWithRoomAsync(connectionId);
    }

    public async Task<Player?> GetPlayerByMobileNumberAsync(string mobileNumber)
    {
        return await _playerRepository.GetByMobileNumberAsync(mobileNumber);
    }

    public async Task<bool> IsPlayerInAnyRoomAsync(string mobileNumber)
    {
        var player = await _playerRepository.GetByMobileNumberAsync(mobileNumber);
        return player?.RoomId != null;
    }

    public async Task<Player?> UpdatePlayerConnectionAsync(string mobileNumber, string newConnectionId)
    {
        var player = await _playerRepository.GetByMobileNumberAsync(mobileNumber);
        if (player != null)
        {
            player.ConnectionId = newConnectionId;
            await _playerRepository.UpdateAsync(player);
        }
        return player;
    }

    public async Task<Room?> GetRoomByIdAsync(int roomId)
    {
        return await _roomRepository.GetByIdAsync(roomId);
    }

    public async Task<Room?> GetRoomWithPlayersAsync(int roomId)
    {
        return await _roomRepository.GetWithPlayersAsync(roomId);
    }

    public async Task RemovePlayerAsync(string connectionId)
    {
        var player = await _playerRepository.GetByConnectionIdWithRoomAsync(connectionId);

        if (player != null)
        {
            var room = player.Room;
            await _playerRepository.DeleteAsync(player);

            if (room != null)
            {
                var remainingPlayers = await _playerRepository.GetPlayerCountByRoomIdAsync(room.Id);
                if (remainingPlayers == 0)
                {
                    room.Status = RoomStatus.Finished;
                    await _roomRepository.UpdateAsync(room);
                }
            }
        }
    }

    public async Task<bool> StartGameAsync(int roomId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);

        if (room == null || room.Players.Count < Room.MaxPlayers)
            return false;

        room.Status = RoomStatus.Playing;
        room.CurrentRound = 1;
        room.CurrentDrawerIndex = 0;

        var players = room.Players.OrderBy(p => p.JoinedAt).ToList();
        foreach (var p in players)
        {
            p.IsDrawing = false;
            p.HasGuessedCorrectly = false;
        }
        players[0].IsDrawing = true;

        var words = _wordService.GetRandomWords(3);
        room.WordOptions = JsonSerializer.Serialize(words);
        room.CurrentWord = null;

        await _roomRepository.UpdateAsync(room);
        return true;
    }

    public async Task<string[]> GetWordOptionsAsync(int roomId)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room?.WordOptions == null)
            return Array.Empty<string>();

        return JsonSerializer.Deserialize<string[]>(room.WordOptions) ?? Array.Empty<string>();
    }

    public async Task SelectWordAsync(int roomId, string word)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null) return;

        room.CurrentWord = word.ToLower().Trim();
        room.RoundStartTime = DateTime.UtcNow;
        await _roomRepository.UpdateAsync(room);
    }

    public async Task<GuessResult> CheckGuessAsync(int roomId, int playerId, string guess)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);

        if (room?.CurrentWord == null || room.RoundStartTime == null)
            return new GuessResult { IsCorrect = false, Points = 0, TimeTaken = 0 };

        var player = room.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.IsDrawing || player.HasGuessedCorrectly)
            return new GuessResult { IsCorrect = false, Points = 0, TimeTaken = 0 };

        var normalizedGuess = guess.ToLower().Trim();
        var normalizedWord = room.CurrentWord.ToLower().Trim();

        if (normalizedGuess != normalizedWord)
            return new GuessResult { IsCorrect = false, Points = 0, TimeTaken = 0 };

        var timeTaken = (DateTime.UtcNow - room.RoundStartTime.Value).TotalSeconds;
        if (timeTaken > Room.RoundDurationSeconds)
            return new GuessResult { IsCorrect = false, Points = 0, TimeTaken = 0 };

        var basePoints = 100;
        var bonusPoints = (int)Math.Max(0, (Room.RoundDurationSeconds - timeTaken) * 2);
        var totalPoints = basePoints + bonusPoints;

        player.HasGuessedCorrectly = true;
        player.GuessTime = DateTime.UtcNow;
        player.Score += totalPoints;

        var gameScore = new GameScore
        {
            PlayerId = playerId,
            RoomId = roomId,
            Round = room.CurrentRound,
            Points = totalPoints,
            GuessedWord = room.CurrentWord,
            TimeTaken = timeTaken
        };
        await _gameScoreRepository.CreateAsync(gameScore);
        await _roomRepository.UpdateAsync(room);

        return new GuessResult { IsCorrect = true, Points = totalPoints, TimeTaken = timeTaken };
    }

    public async Task AwardDrawerPointsAsync(int roomId, int guesserPoints, double timeTaken)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return;

        var drawer = room.Players.FirstOrDefault(p => p.IsDrawing);
        if (drawer == null) return;

        var drawerPoints = (int)Math.Max(0, (Room.RoundDurationSeconds - timeTaken) * 3);
        drawer.Score += drawerPoints;

        await _roomRepository.UpdateAsync(room);
    }

    public async Task<bool> NextTurnAsync(int roomId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return false;

        var players = room.Players.OrderBy(p => p.JoinedAt).ToList();

        foreach (var p in players)
        {
            p.IsDrawing = false;
            p.HasGuessedCorrectly = false;
            p.GuessTime = null;
        }

        room.CurrentDrawerIndex++;

        if (room.CurrentDrawerIndex >= players.Count)
        {
            room.CurrentDrawerIndex = 0;
            room.CurrentRound++;

            if (room.CurrentRound > room.TotalRounds)
            {
                room.Status = RoomStatus.Finished;
                await _roomRepository.UpdateAsync(room);
                return false;
            }
        }

        players[room.CurrentDrawerIndex].IsDrawing = true;

        var words = _wordService.GetRandomWords(3);
        room.WordOptions = JsonSerializer.Serialize(words);
        room.CurrentWord = null;
        room.RoundStartTime = null;

        await _roomRepository.UpdateAsync(room);
        return true;
    }

    public async Task<List<PlayerScoreModel>> GetScoresAsync(int roomId)
    {
        var players = await _playerRepository.GetByRoomIdAsync(roomId);

        return players.Select(p => new PlayerScoreModel
        {
            PlayerId = p.Id,
            Username = p.Username,
            Score = p.Score,
            IsDrawing = p.IsDrawing,
            HasGuessedCorrectly = p.HasGuessedCorrectly
        }).ToList();
    }

    public async Task ResetPlayerGuessStatusAsync(int roomId)
    {
        var players = await _playerRepository.GetByRoomIdAsync(roomId);

        foreach (var player in players)
        {
            player.HasGuessedCorrectly = false;
            player.GuessTime = null;
            await _playerRepository.UpdateAsync(player);
        }
    }

    public async Task<Player?> GetCurrentDrawerAsync(int roomId)
    {
        return await _playerRepository.GetCurrentDrawerAsync(roomId);
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
