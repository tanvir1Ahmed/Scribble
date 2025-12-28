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
                    RoomType = RoomType.Public,
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

    public async Task<Player> JoinRoomAsync(string connectionId, string username, string mobileNumber, int roomId, int? userId = null, bool isHost = false)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);

        if (room == null)
            throw new Exception("Room not found");

        if (room.Players.Count >= room.MaxPlayers)
            throw new Exception("Room is full");

        // IMPORTANT: First, delete ANY existing player record for this mobile number (in ANY room)
        // This prevents duplicate player records across rooms
        var existingPlayerAnywhere = await _playerRepository.GetByMobileNumberAsync(mobileNumber);
        if (existingPlayerAnywhere != null)
        {
            Console.WriteLine($"[JoinRoomAsync] Found existing player {existingPlayerAnywhere.Id} in room {existingPlayerAnywhere.RoomId}, deleting before joining room {roomId}");
            await _playerRepository.DeleteAsync(existingPlayerAnywhere);
        }

        // Now create fresh player for this room
        var player = new Player
        {
            ConnectionId = connectionId,
            Username = username,
            MobileNumber = mobileNumber,
            UserId = userId,
            RoomId = roomId,
            Score = 0,
            IsDrawing = false,
            IsHost = isHost,
            HasGuessedCorrectly = false,
            JoinedAt = DateTime.UtcNow
        };

        var createdPlayer = await _playerRepository.CreateAsync(player);
        Console.WriteLine($"[JoinRoomAsync] Created player {createdPlayer.Id} in room {roomId}, isHost: {isHost}");

        // If this is the host, update the room's host reference
        if (isHost)
        {
            room.HostPlayerId = createdPlayer.Id;
            await _roomRepository.UpdateAsync(room);
        }

        return createdPlayer;
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

    public async Task RemovePlayerByMobileNumberAsync(string mobileNumber)
    {
        var player = await _playerRepository.GetByMobileNumberAsync(mobileNumber);
        if (player != null)
        {
            Console.WriteLine($"[RemovePlayerByMobileNumber] Removing player {player.Id} ({player.Username}) from room {player.RoomId}");
            await _playerRepository.DeleteAsync(player);
        }
    }

    public async Task<bool> StartGameAsync(int roomId)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        
        // Use direct player count instead of room.Players.Count
        var playerCount = await _playerRepository.GetPlayerCountByRoomIdAsync(roomId);
        
        Console.WriteLine($"[StartGameAsync] RoomId: {roomId}, PlayerCount (direct): {playerCount}, MinPlayers: {room?.MinPlayers ?? 0}");

        if (room == null || playerCount < room.MinPlayers)
        {
            Console.WriteLine($"[StartGameAsync] Cannot start - room null or not enough players");
            return false;
        }

        room.Status = RoomStatus.Playing;
        room.CurrentRound = 1;
        room.CurrentDrawerIndex = 0;

        // Get players directly from repository
        var players = await _playerRepository.GetByRoomIdAsync(roomId);
        players = players.OrderBy(p => p.JoinedAt).ToList();
        
        foreach (var p in players)
        {
            p.IsDrawing = false;
            p.HasGuessedCorrectly = false;
            await _playerRepository.UpdateAsync(p);
        }
        
        if (players.Count > 0)
        {
            players[0].IsDrawing = true;
            await _playerRepository.UpdateAsync(players[0]);
        }

        var words = _wordService.GetRandomWords(3);
        room.WordOptions = JsonSerializer.Serialize(words);
        room.CurrentWord = null;

        await _roomRepository.UpdateAsync(room);
        Console.WriteLine($"[StartGameAsync] Game started successfully for room {roomId}");
        return true;
    }

    public async Task<bool> CanStartGameAsync(int roomId)
    {
        var room = await _roomRepository.GetWithPlayersNoTrackingAsync(roomId);
        Console.WriteLine($"[CanStartGame] RoomId: {roomId}, Room found: {room != null}, Players: {room?.Players.Count ?? 0}, MinPlayers: {room?.MinPlayers ?? 0}, Status: {room?.Status}");
        return room != null && room.Players.Count >= room.MinPlayers && room.Status == RoomStatus.Waiting;
    }

    public async Task<(bool CanStart, string? Error)> CanStartGameWithReasonAsync(int roomId)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        
        if (room == null)
        {
            return (false, "Room not found");
        }
        
        // IMPORTANT: Use direct player count query instead of room.Players.Count
        // The Include/navigation property is unreliable - query players directly
        var playerCount = await _playerRepository.GetPlayerCountByRoomIdAsync(roomId);
        
        Console.WriteLine($"[CanStartGameWithReason] RoomId: {roomId}, PlayerCount (direct query): {playerCount}, MinPlayers: {room.MinPlayers}, Status: {room.Status}");
        
        if (room.Status != RoomStatus.Waiting)
        {
            return (false, $"Game is already {room.Status.ToString().ToLower()}");
        }
        
        if (playerCount < room.MinPlayers)
        {
            return (false, $"Not enough players. Need at least {room.MinPlayers}, but only {playerCount} in room");
        }
        
        return (true, null);
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
        if (timeTaken > room.RoundDurationSeconds)
            return new GuessResult { IsCorrect = false, Points = 0, TimeTaken = 0 };

        var basePoints = 100;
        var bonusPoints = (int)Math.Max(0, (room.RoundDurationSeconds - timeTaken) * 2);
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

        var drawerPoints = (int)Math.Max(0, (room.RoundDurationSeconds - timeTaken) * 3);
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
            HasGuessedCorrectly = p.HasGuessedCorrectly,
            IsHost = p.IsHost
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

    // New methods for custom rooms

    public async Task<CreateRoomResult> CreateCustomRoomAsync(string connectionId, string username, string mobileNumber, int? userId, CreateRoomSettings settings)
    {
        // Validate settings
        if (settings.MaxPlayers < Room.MinPlayersLimit || settings.MaxPlayers > Room.MaxPlayersLimit)
        {
            return new CreateRoomResult { Success = false, Error = $"Max players must be between {Room.MinPlayersLimit} and {Room.MaxPlayersLimit}" };
        }

        if (settings.TotalRounds < Room.MinRoundsLimit || settings.TotalRounds > Room.MaxRoundsLimit)
        {
            return new CreateRoomResult { Success = false, Error = $"Rounds must be between {Room.MinRoundsLimit} and {Room.MaxRoundsLimit}" };
        }

        if (settings.RoundDurationSeconds < Room.MinDurationSeconds || settings.RoundDurationSeconds > Room.MaxDurationSeconds)
        {
            return new CreateRoomResult { Success = false, Error = $"Round duration must be between {Room.MinDurationSeconds} and {Room.MaxDurationSeconds} seconds" };
        }

        await _roomLock.WaitAsync();
        try
        {
            var roomCode = GenerateRoomCode();
            // Ensure unique room code
            while (await _roomRepository.RoomCodeExistsAsync(roomCode))
            {
                roomCode = GenerateRoomCode();
            }

            var room = new Room
            {
                RoomCode = roomCode,
                Status = RoomStatus.Waiting,
                RoomType = RoomType.Private,
                MinPlayers = Room.MinPlayersLimit,
                MaxPlayers = settings.MaxPlayers,
                TotalRounds = settings.TotalRounds,
                RoundDurationSeconds = settings.RoundDurationSeconds,
                HintLettersCount = settings.HintLettersCount,
                CustomHintsEnabled = settings.CustomHintsEnabled,
                CreatedAt = DateTime.UtcNow
            };

            room = await _roomRepository.CreateAsync(room);

            return new CreateRoomResult
            {
                Success = true,
                RoomId = room.Id,
                RoomCode = room.RoomCode
            };
        }
        finally
        {
            _roomLock.Release();
        }
    }

    public async Task<JoinRoomResult> JoinRoomByCodeAsync(string connectionId, string username, string mobileNumber, int? userId, string roomCode)
    {
        var room = await _roomRepository.GetWithPlayersByCodeAsync(roomCode);

        if (room == null)
        {
            return new JoinRoomResult { Success = false, Error = "Room not found" };
        }

        if (room.Status == RoomStatus.Finished)
        {
            return new JoinRoomResult { Success = false, Error = "Room is no longer available" };
        }

        if (room.Status == RoomStatus.Playing)
        {
            return new JoinRoomResult { Success = false, Error = "Game already in progress" };
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            return new JoinRoomResult { Success = false, Error = "Room is full" };
        }

        // IMPORTANT: Delete any existing player record for this mobile number (in ANY room)
        var existingPlayerAnywhere = await _playerRepository.GetByMobileNumberAsync(mobileNumber);
        if (existingPlayerAnywhere != null)
        {
            Console.WriteLine($"[JoinRoomByCodeAsync] Found existing player {existingPlayerAnywhere.Id} in room {existingPlayerAnywhere.RoomId}, deleting before joining room {room.Id}");
            await _playerRepository.DeleteAsync(existingPlayerAnywhere);
        }

        var player = new Player
        {
            ConnectionId = connectionId,
            Username = username,
            MobileNumber = mobileNumber,
            UserId = userId,
            RoomId = room.Id,
            Score = 0,
            IsDrawing = false,
            IsHost = false,
            HasGuessedCorrectly = false,
            JoinedAt = DateTime.UtcNow
        };

        var createdPlayer = await _playerRepository.CreateAsync(player);
        Console.WriteLine($"[JoinRoomByCodeAsync] Created player {createdPlayer.Id} in room {room.Id}");

        return new JoinRoomResult
        {
            Success = true,
            RoomId = room.Id,
            RoomCode = room.RoomCode,
            PlayerId = createdPlayer.Id,
            IsHost = false
        };
    }

    public async Task<Room?> GetRoomByCodeAsync(string roomCode)
    {
        return await _roomRepository.GetWithPlayersByCodeAsync(roomCode);
    }

    public async Task<bool> RemovePlayerFromRoomAsync(int roomId, int playerId, int hostPlayerId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return false;

        // Verify the requester is the host
        var host = room.Players.FirstOrDefault(p => p.Id == hostPlayerId);
        if (host == null || !host.IsHost) return false;

        // Cannot remove yourself as host
        if (playerId == hostPlayerId) return false;

        var playerToRemove = room.Players.FirstOrDefault(p => p.Id == playerId);
        if (playerToRemove == null) return false;

        await _playerRepository.DeleteAsync(playerToRemove);
        return true;
    }

    public async Task<bool> RestartGameAsync(int roomId, int hostPlayerId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return false;

        // Verify the requester is the host
        var host = room.Players.FirstOrDefault(p => p.Id == hostPlayerId);
        if (host == null || !host.IsHost) return false;

        // Reset room state
        room.Status = RoomStatus.Waiting;
        room.CurrentRound = 0;
        room.CurrentDrawerIndex = 0;
        room.CurrentWord = null;
        room.WordOptions = null;
        room.RoundStartTime = null;

        // Reset all players' scores
        foreach (var player in room.Players)
        {
            player.Score = 0;
            player.IsDrawing = false;
            player.HasGuessedCorrectly = false;
            player.GuessTime = null;
        }

        await _roomRepository.UpdateAsync(room);
        return true;
    }

    public async Task<string> GenerateHintAsync(string word, int hintLettersCount, bool customHintsEnabled)
    {
        if (string.IsNullOrEmpty(word)) return string.Empty;

        var hint = new char[word.Length];
        var wordLower = word.ToLower();

        // Always show first letter
        hint[0] = wordLower[0];

        // Fill the rest with underscores initially
        for (int i = 1; i < word.Length; i++)
        {
            hint[i] = '_';
        }

        if (customHintsEnabled && hintLettersCount > 1 && word.Length > 2)
        {
            // Reveal additional random letters
            var availableIndices = Enumerable.Range(1, word.Length - 1).ToList();
            var random = new Random();
            var lettersToReveal = Math.Min(hintLettersCount - 1, availableIndices.Count);

            for (int i = 0; i < lettersToReveal; i++)
            {
                var idx = random.Next(availableIndices.Count);
                var letterIndex = availableIndices[idx];
                hint[letterIndex] = wordLower[letterIndex];
                availableIndices.RemoveAt(idx);
            }
        }

        return string.Join(" ", hint);
    }

    public async Task<bool> IsPlayerHostAsync(int roomId, int playerId)
    {
        var room = await _roomRepository.GetWithPlayersAsync(roomId);
        if (room == null) return false;

        var player = room.Players.FirstOrDefault(p => p.Id == playerId);
        return player?.IsHost ?? false;
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
