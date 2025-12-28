using Microsoft.AspNetCore.SignalR;
using Scribble.Business.Interfaces;
using Scribble.Business.Models;
using Scribble.Repository.Data.Entities;
using Scribble.Repository.Interfaces;
using Scribble_API.DTOs;
using System.Security.Claims;

namespace Scribble_API.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IPlayerRoomCacheService _playerRoomCache;
    private readonly IFriendService _friendService;
    private readonly IRoomInvitationService _invitationService;
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private static readonly Dictionary<int, Timer> _roomTimers = new();
    private static readonly object _timerLock = new();

    public GameHub(
        IGameService gameService, 
        ILeaderboardService leaderboardService, 
        IServiceScopeFactory serviceScopeFactory, 
        IHubContext<GameHub> hubContext,
        IPlayerRoomCacheService playerRoomCache,
        IFriendService friendService,
        IRoomInvitationService invitationService,
        IAuthService authService,
        IUserRepository userRepository)
    {
        _gameService = gameService;
        _leaderboardService = leaderboardService;
        _serviceScopeFactory = serviceScopeFactory;
        _hubContext = hubContext;
        _playerRoomCache = playerRoomCache;
        _friendService = friendService;
        _invitationService = invitationService;
        _authService = authService;
        _userRepository = userRepository;
    }

    private string? GetMobileNumber()
    {
        return Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value 
            ?? Context.User?.FindFirst("mobile_number")?.Value;
    }

    private async Task<int?> GetCurrentUserIdAsync()
    {
        var mobileNumber = GetMobileNumber();
        if (string.IsNullOrEmpty(mobileNumber)) return null;
        var user = await _authService.GetUserByMobileNumberAsync(mobileNumber);
        return user?.Id;
    }

    public override async Task OnConnectedAsync()
    {
        var mobileNumber = GetMobileNumber();
        if (!string.IsNullOrEmpty(mobileNumber))
        {
            var user = await _authService.GetUserByMobileNumberAsync(mobileNumber);
            if (user != null)
            {
                await _userRepository.SetOnlineStatusAsync(user.Id, true, Context.ConnectionId);
                
                // Notify friends that this user is online
                var friends = await _friendService.GetFriendsAsync(user.Id);
                foreach (var friend in friends)
                {
                    var friendUser = await _authService.GetUserByIdAsync(friend.UserId);
                    if (friendUser?.CurrentConnectionId != null)
                    {
                        await _hubContext.Clients.Client(friendUser.CurrentConnectionId)
                            .SendAsync("FriendOnlineStatusChanged", new { userId = user.Id, username = user.Username, isOnline = true });
                    }
                }
            }

            // Check if player is in a room and re-add them to the group
            var roomInfo = await _playerRoomCache.GetPlayerRoomAsync(mobileNumber);
            if (roomInfo != null)
            {
                var room = await _gameService.GetRoomWithPlayersAsync(roomInfo.RoomId);
                if (room != null && room.Status != RoomStatus.Finished)
                {
                    Console.WriteLine($"[OnConnectedAsync] Player {mobileNumber} reconnecting to room {room.Id}");
                    // Update connection ID in cache and database
                    await _playerRoomCache.UpdateConnectionIdAsync(mobileNumber, Context.ConnectionId);
                    await _gameService.UpdatePlayerConnectionAsync(mobileNumber, Context.ConnectionId);
                    // Re-add to SignalR group
                    await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.ToString());
                    Console.WriteLine($"[OnConnectedAsync] Player added to group {room.Id}");
                }
            }
        }
        await base.OnConnectedAsync();
    }

    public async Task<object?> CheckPlayerRoom()
    {
        var mobileNumber = GetMobileNumber();
        if (string.IsNullOrEmpty(mobileNumber))
            return new { inRoom = false, error = "Not authenticated" };

        var roomInfo = await _playerRoomCache.GetPlayerRoomAsync(mobileNumber);
        if (roomInfo != null)
        {
            // Verify room still exists and is valid
            var room = await _gameService.GetRoomWithPlayersAsync(roomInfo.RoomId);
            if (room != null && room.Status != RoomStatus.Finished)
            {
                return new 
                { 
                    inRoom = true, 
                    roomId = roomInfo.RoomId,
                    roomCode = roomInfo.RoomCode,
                    username = roomInfo.Username
                };
            }
            else
            {
                // Room no longer valid, clear cache
                await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
            }
        }

        return new { inRoom = false };
    }

    public async Task JoinGame(string username)
    {
        var mobileNumber = GetMobileNumber();
        if (string.IsNullOrEmpty(mobileNumber))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required" });
            return;
        }

        // Check if player is already in a room
        var existingRoomInfo = await _playerRoomCache.GetPlayerRoomAsync(mobileNumber);
        if (existingRoomInfo != null)
        {
            var existingRoom = await _gameService.GetRoomWithPlayersAsync(existingRoomInfo.RoomId);
            if (existingRoom != null && existingRoom.Status != RoomStatus.Finished)
            {
                // Update connection ID and rejoin
                await _playerRoomCache.UpdateConnectionIdAsync(mobileNumber, Context.ConnectionId);
                await _gameService.UpdatePlayerConnectionAsync(mobileNumber, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, existingRoom.Id.ToString());

                var scores = await _gameService.GetScoresAsync(existingRoom.Id);

                // Notify player they're rejoining
                await Clients.Caller.SendAsync("RejoinedRoom", new 
                {
                    RoomId = existingRoom.Id,
                    RoomCode = existingRoom.RoomCode,
                    Status = existingRoom.Status.ToString(),
                    Username = existingRoomInfo.Username,
                    Players = scores.Select(s => new PlayerScoreDto
                    {
                        PlayerId = s.PlayerId,
                        Username = s.Username,
                        Score = s.Score,
                        IsDrawing = s.IsDrawing,
                        HasGuessedCorrectly = s.HasGuessedCorrectly
                    }).ToList()
                });
                return;
            }
            else
            {
                // Room no longer valid, clear cache
                await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
            }
        }

        var room = await _gameService.FindOrCreateRoomAsync();
        var player = await _gameService.JoinRoomAsync(Context.ConnectionId, username, mobileNumber, room.Id);

        // Cache the player-room mapping
        await _playerRoomCache.SetPlayerRoomAsync(mobileNumber, new PlayerRoomInfo
        {
            RoomId = room.Id,
            RoomCode = room.RoomCode,
            Username = username,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTime.UtcNow
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.ToString());

        // Notify the player of their info
        await Clients.Caller.SendAsync("PlayerJoined", new PlayerJoinedDto
        {
            PlayerId = player.Id,
            Username = player.Username,
            RoomId = room.Id,
            RoomCode = room.RoomCode
        });

        // Get updated room info
        var updatedRoom = await _gameService.GetRoomWithPlayersAsync(room.Id);
        var updatedScores = await _gameService.GetScoresAsync(room.Id);

        // Notify all players in the room
        await Clients.Group(room.Id.ToString()).SendAsync("RoomUpdated", new RoomDto
        {
            RoomId = room.Id,
            RoomCode = room.RoomCode,
            PlayerCount = updatedRoom?.Players.Count ?? 0,
            MinPlayers = updatedRoom?.MinPlayers ?? Room.DefaultMinPlayers,
            MaxPlayers = updatedRoom?.MaxPlayers ?? Room.DefaultMaxPlayers,
            TotalRounds = updatedRoom?.TotalRounds ?? Room.DefaultRounds,
            RoundDurationSeconds = updatedRoom?.RoundDurationSeconds ?? Room.DefaultRoundDuration,
            RoomType = updatedRoom?.RoomType.ToString() ?? "Public",
            Status = updatedRoom?.Status.ToString() ?? "Unknown",
            CustomHintsEnabled = updatedRoom?.CustomHintsEnabled ?? false,
            HintLettersCount = updatedRoom?.HintLettersCount ?? Room.DefaultHintLetters,
            Players = updatedScores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly,
                IsHost = s.IsHost
            }).ToList()
        });

        // Check if room is full and start the game (for public rooms)
        if (updatedRoom?.RoomType == RoomType.Public && updatedRoom.Players.Count >= updatedRoom.MaxPlayers)
        {
            await StartGame(room.Id);
        }
    }

    public async Task LeaveRoom()
    {
        var mobileNumber = GetMobileNumber();
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);

        if (player?.RoomId != null)
        {
            var roomId = player.RoomId.Value;
            var wasDrawing = player.IsDrawing;
            var playerUsername = player.Username;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
            await _gameService.RemovePlayerAsync(Context.ConnectionId);

            // Clear cache
            if (!string.IsNullOrEmpty(mobileNumber))
            {
                await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
            }

            // Notify remaining players
            var room = await _gameService.GetRoomWithPlayersAsync(roomId);
            if (room != null && room.Players.Count > 0)
            {
                var scores = await _gameService.GetScoresAsync(roomId);

                await Clients.Group(roomId.ToString()).SendAsync("PlayerLeft", new PlayerLeftDto
                {
                    Username = playerUsername,
                    Players = scores.Select(s => new PlayerScoreDto
                    {
                        PlayerId = s.PlayerId,
                        Username = s.Username,
                        Score = s.Score,
                        IsDrawing = s.IsDrawing,
                        HasGuessedCorrectly = s.HasGuessedCorrectly
                    }).ToList(),
                    PlayerCount = room.Players.Count
                });

                // If the drawer left during a game, move to next turn
                if (wasDrawing && room.Status == RoomStatus.Playing)
                {
                    lock (_timerLock)
                    {
                        if (_roomTimers.TryGetValue(roomId, out var timer))
                        {
                            timer.Dispose();
                            _roomTimers.Remove(roomId);
                        }
                    }

                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
                        await NextTurnAsync(roomId, gameService);
                    });
                }
            }

            // Notify the leaving player
            await Clients.Caller.SendAsync("LeftRoom", new { success = true });
        }
    }

    private async Task StartGame(int roomId)
    {
        Console.WriteLine($"[StartGame] Starting game for room {roomId}");
        var success = await _gameService.StartGameAsync(roomId);
        Console.WriteLine($"[StartGame] StartGameAsync returned: {success}");
        if (!success) return;

        var room = await _gameService.GetRoomWithPlayersAsync(roomId);
        Console.WriteLine($"[StartGame] Room fetched: {room != null}, Players: {room?.Players.Count ?? 0}");
        if (room == null) return;

        var drawer = room.Players.FirstOrDefault(p => p.IsDrawing);
        Console.WriteLine($"[StartGame] Drawer: {drawer?.Username ?? "null"}");
        var wordOptions = await _gameService.GetWordOptionsAsync(roomId);
        var scores = await _gameService.GetScoresAsync(roomId);

        Console.WriteLine($"[StartGame] Sending GameStarted to group {roomId}");
        // Notify all players that game started
        await Clients.Group(roomId.ToString()).SendAsync("GameStarted", new GameStartedDto
        {
            RoomId = room.Id,
            CurrentRound = room.CurrentRound,
            TotalRounds = room.TotalRounds,
            DrawerId = drawer?.Id,
            DrawerName = drawer?.Username,
            Players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList()
        });
        Console.WriteLine($"[StartGame] GameStarted event sent");

        // Send word options only to the drawer
        if (drawer != null)
        {
            Console.WriteLine($"[StartGame] Sending SelectWord to drawer {drawer.ConnectionId}");
            await Clients.Client(drawer.ConnectionId).SendAsync("SelectWord", wordOptions);
        }
    }

    public async Task WordSelected(string word)
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null || !player.IsDrawing) return;

        await _gameService.SelectWordAsync(player.RoomId.Value, word);

        var room = await _gameService.GetRoomWithPlayersAsync(player.RoomId.Value);
        if (room == null) return;

        // Generate hint based on room settings
        var hint = await _gameService.GenerateHintAsync(word, room.HintLettersCount, room.CustomHintsEnabled);

        // Notify all players that drawing has started
        await Clients.Group(player.RoomId.Value.ToString()).SendAsync("DrawingStarted", new DrawingStartedDto
        {
            Hint = hint,
            WordLength = word.Length,
            Duration = room.RoundDurationSeconds
        });

        // Start the timer with room-specific duration
        StartRoundTimer(player.RoomId.Value, room.RoundDurationSeconds);
    }

    private void StartRoundTimer(int roomId, int durationSeconds = Room.DefaultRoundDuration)
    {
        lock (_timerLock)
        {
            // Cancel existing timer if any
            if (_roomTimers.TryGetValue(roomId, out var existingTimer))
            {
                existingTimer.Dispose();
                _roomTimers.Remove(roomId);
            }

            var timer = new Timer(async _ =>
            {
                await RoundTimeUpAsync(roomId);
            }, null, durationSeconds * 1000, Timeout.Infinite);

            _roomTimers[roomId] = timer;
        }
    }

    private async Task RoundTimeUpAsync(int roomId)
    {
        lock (_timerLock)
        {
            if (_roomTimers.TryGetValue(roomId, out var timer))
            {
                timer.Dispose();
                _roomTimers.Remove(roomId);
            }
        }

        // Create a new scope for database operations
        using var scope = _serviceScopeFactory.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

        var room = await gameService.GetRoomByIdAsync(roomId);
        if (room == null) return;

        var scores = await gameService.GetScoresAsync(roomId);

        // Notify all players that time is up
        await _hubContext.Clients.Group(roomId.ToString()).SendAsync("TimeUp", new TimeUpDto
        {
            CorrectWord = room.CurrentWord,
            Players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList()
        });

        // Wait a bit before next turn
        await Task.Delay(3000);

        // Move to next turn
        await NextTurnAsync(roomId, gameService);
    }

    private async Task NextTurnAsync(int roomId, IGameService gameService)
    {
        var continueGame = await gameService.NextTurnAsync(roomId);

        if (!continueGame)
        {
            // Game ended - record leaderboard stats
            using var leaderboardScope = _serviceScopeFactory.CreateScope();
            var leaderboardService = leaderboardScope.ServiceProvider.GetRequiredService<ILeaderboardService>();
            await leaderboardService.RecordGameEndAsync(roomId);

            var finalScores = await gameService.GetScoresAsync(roomId);
            await _hubContext.Clients.Group(roomId.ToString()).SendAsync("GameEnded", new GameEndedDto
            {
                Players = finalScores.OrderByDescending(p => p.Score).Select(s => new PlayerScoreDto
                {
                    PlayerId = s.PlayerId,
                    Username = s.Username,
                    Score = s.Score,
                    IsDrawing = s.IsDrawing,
                    HasGuessedCorrectly = s.HasGuessedCorrectly
                }).ToList()
            });
            return;
        }

        var room = await gameService.GetRoomWithPlayersAsync(roomId);
        if (room == null) return;

        var drawer = room.Players.FirstOrDefault(p => p.IsDrawing);
        var wordOptions = await gameService.GetWordOptionsAsync(roomId);
        var scores = await gameService.GetScoresAsync(roomId);

        // Notify all players of new round
        await _hubContext.Clients.Group(roomId.ToString()).SendAsync("NewTurn", new NewTurnDto
        {
            CurrentRound = room.CurrentRound,
            TotalRounds = room.TotalRounds,
            DrawerId = drawer?.Id,
            DrawerName = drawer?.Username,
            Players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList()
        });

        // Send word options only to the drawer
        if (drawer != null)
        {
            await _hubContext.Clients.Client(drawer.ConnectionId).SendAsync("SelectWord", wordOptions);
        }
    }

    public async Task Draw(object drawData)
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null || !player.IsDrawing) return;

        // Broadcast draw data to all other players in the room
        await Clients.OthersInGroup(player.RoomId.Value.ToString()).SendAsync("Draw", drawData);
    }

    public async Task ClearCanvas()
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null || !player.IsDrawing) return;

        await Clients.Group(player.RoomId.Value.ToString()).SendAsync("ClearCanvas");
    }

    public async Task SendMessage(string message)
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null) return;

        // If player is drawing, they can't guess
        if (player.IsDrawing)
        {
            await Clients.Caller.SendAsync("ChatMessage", new ChatMessageDto
            {
                Username = "System",
                Message = "You can't chat while drawing!",
                IsSystem = true
            });
            return;
        }

        // If player already guessed, don't show their messages (to prevent hints)
        if (player.HasGuessedCorrectly)
        {
            await Clients.Caller.SendAsync("ChatMessage", new ChatMessageDto
            {
                Username = "System",
                Message = "You already guessed correctly! Wait for the next round.",
                IsSystem = true
            });
            return;
        }

        // Check if the guess is correct
        var result = await _gameService.CheckGuessAsync(player.RoomId.Value, player.Id, message);

        if (result.IsCorrect)
        {
            // Award points to drawer
            await _gameService.AwardDrawerPointsAsync(player.RoomId.Value, result.Points, result.TimeTaken);

            // Don't show the correct word to others
            await Clients.Group(player.RoomId.Value.ToString()).SendAsync("ChatMessage", new ChatMessageDto
            {
                Username = player.Username,
                Message = $"guessed the word! (+{result.Points} points)",
                IsCorrect = true,
                IsSystem = false
            });

            // Update scores
            var scores = await _gameService.GetScoresAsync(player.RoomId.Value);
            await Clients.Group(player.RoomId.Value.ToString()).SendAsync("ScoresUpdated", scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList());

            // Check if all players have guessed
            var room = await _gameService.GetRoomWithPlayersAsync(player.RoomId.Value);
            if (room != null)
            {
                var allGuessed = room.Players.Where(p => !p.IsDrawing).All(p => p.HasGuessedCorrectly);
                if (allGuessed)
                {
                    // End round early
                    var roomId = player.RoomId.Value;
                    var currentWord = room.CurrentWord;

                    lock (_timerLock)
                    {
                        if (_roomTimers.TryGetValue(roomId, out var timer))
                        {
                            timer.Dispose();
                            _roomTimers.Remove(roomId);
                        }
                    }

                    await Clients.Group(roomId.ToString()).SendAsync("TimeUp", new TimeUpDto
                    {
                        CorrectWord = currentWord,
                        Players = scores.Select(s => new PlayerScoreDto
                        {
                            PlayerId = s.PlayerId,
                            Username = s.Username,
                            Score = s.Score,
                            IsDrawing = s.IsDrawing,
                            HasGuessedCorrectly = s.HasGuessedCorrectly
                        }).ToList(),
                        AllGuessed = true
                    });

                    // Start background task for next turn
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        using var scope = _serviceScopeFactory.CreateScope();
                        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
                        await NextTurnAsync(roomId, gameService);
                    });
                }
            }
        }
        else
        {
            // Show the message to all players
            await Clients.Group(player.RoomId.Value.ToString()).SendAsync("ChatMessage", new ChatMessageDto
            {
                Username = player.Username,
                Message = message,
                IsCorrect = false,
                IsSystem = false
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var mobileNumber = GetMobileNumber();
        
        // Update user online status
        if (!string.IsNullOrEmpty(mobileNumber))
        {
            var user = await _authService.GetUserByMobileNumberAsync(mobileNumber);
            if (user != null)
            {
                await _userRepository.SetOnlineStatusAsync(user.Id, false, null);
                
                // Notify friends that this user is offline
                var friends = await _friendService.GetFriendsAsync(user.Id);
                foreach (var friend in friends)
                {
                    var friendUser = await _authService.GetUserByIdAsync(friend.UserId);
                    if (friendUser?.CurrentConnectionId != null)
                    {
                        await _hubContext.Clients.Client(friendUser.CurrentConnectionId)
                            .SendAsync("FriendOnlineStatusChanged", new { userId = user.Id, username = user.Username, isOnline = false });
                    }
                }
            }
        }

        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);

        if (player?.RoomId != null)
        {
            var roomId = player.RoomId.Value;
            var wasDrawing = player.IsDrawing;
            var playerUsername = player.Username;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
            await _gameService.RemovePlayerAsync(Context.ConnectionId);

            // Clear cache for this player
            if (!string.IsNullOrEmpty(mobileNumber))
            {
                await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
            }

            // Notify remaining players
            var room = await _gameService.GetRoomWithPlayersAsync(roomId);
            if (room != null && room.Players.Count > 0)
            {
                var scores = await _gameService.GetScoresAsync(roomId);

                await Clients.Group(roomId.ToString()).SendAsync("PlayerLeft", new PlayerLeftDto
                {
                    Username = playerUsername,
                    Players = scores.Select(s => new PlayerScoreDto
                    {
                        PlayerId = s.PlayerId,
                        Username = s.Username,
                        Score = s.Score,
                        IsDrawing = s.IsDrawing,
                        HasGuessedCorrectly = s.HasGuessedCorrectly,
                        IsHost = s.IsHost
                    }).ToList(),
                    PlayerCount = room.Players.Count
                });

                // If the drawer left, move to next turn
                if (wasDrawing && room.Status == RoomStatus.Playing)
                {
                    lock (_timerLock)
                    {
                        if (_roomTimers.TryGetValue(roomId, out var timer))
                        {
                            timer.Dispose();
                            _roomTimers.Remove(roomId);
                        }
                    }

                    // Start background task for next turn
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
                        await NextTurnAsync(roomId, gameService);
                    });
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ===== NEW METHODS FOR CUSTOM ROOMS =====

    /// <summary>
    /// Create a custom private room with settings
    /// </summary>
    public async Task<CreateRoomResponseDto> CreateRoom(CreateRoomRequestDto request)
    {
        var mobileNumber = GetMobileNumber();
        if (string.IsNullOrEmpty(mobileNumber))
        {
            return new CreateRoomResponseDto { Success = false, Error = "Authentication required" };
        }

        var userId = await GetCurrentUserIdAsync();
        var user = await _authService.GetUserByMobileNumberAsync(mobileNumber);
        var username = user?.Username ?? "Player";

        // IMPORTANT: Clean up any existing player record before creating new room
        var existingPlayer = await _gameService.GetPlayerByMobileNumberAsync(mobileNumber);
        if (existingPlayer != null)
        {
            Console.WriteLine($"[CreateRoom] Cleaning up existing player {existingPlayer.Id} from room {existingPlayer.RoomId}");
            if (existingPlayer.RoomId.HasValue)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, existingPlayer.RoomId.Value.ToString());
            }
            await _gameService.RemovePlayerByMobileNumberAsync(mobileNumber);
            await _playerRoomCache.RemovePlayerRoomAsync(mobileNumber);
        }

        var settings = new CreateRoomSettings
        {
            MaxPlayers = request.MaxPlayers,
            TotalRounds = request.TotalRounds,
            RoundDurationSeconds = request.RoundDurationSeconds,
            HintLettersCount = request.HintLettersCount,
            CustomHintsEnabled = request.CustomHintsEnabled
        };

        var result = await _gameService.CreateCustomRoomAsync(
            Context.ConnectionId, username, mobileNumber, userId, settings);

        if (!result.Success)
        {
            return new CreateRoomResponseDto { Success = false, Error = result.Error };
        }

        // Join the room as host
        var player = await _gameService.JoinRoomAsync(
            Context.ConnectionId, username, mobileNumber, result.RoomId!.Value, userId, isHost: true);

        // Cache the player-room mapping
        await _playerRoomCache.SetPlayerRoomAsync(mobileNumber, new PlayerRoomInfo
        {
            RoomId = result.RoomId.Value,
            RoomCode = result.RoomCode!,
            Username = username,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTime.UtcNow
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, result.RoomId.Value.ToString());

        // Get room details to send settings
        var room = await _gameService.GetRoomWithPlayersAsync(result.RoomId.Value);

        // Notify the host
        await Clients.Caller.SendAsync("RoomCreated", new
        {
            roomId = result.RoomId,
            roomCode = result.RoomCode,
            playerId = player.Id,
            isHost = true,
            maxPlayers = room?.MaxPlayers ?? 8,
            minPlayers = room?.MinPlayers ?? 2,
            totalRounds = room?.TotalRounds ?? 3,
            roundDurationSeconds = room?.RoundDurationSeconds ?? 120
        });

        return new CreateRoomResponseDto
        {
            Success = true,
            RoomId = result.RoomId,
            RoomCode = result.RoomCode
        };
    }

    /// <summary>
    /// Join an existing room by room code
    /// </summary>
    public async Task<JoinRoomResponseDto> JoinRoomByCode(string roomCode)
    {
        var mobileNumber = GetMobileNumber();
        if (string.IsNullOrEmpty(mobileNumber))
        {
            return new JoinRoomResponseDto { Success = false, Error = "Authentication required" };
        }

        var userId = await GetCurrentUserIdAsync();
        var user = await _authService.GetUserByMobileNumberAsync(mobileNumber);
        var username = user?.Username ?? "Player";

        var result = await _gameService.JoinRoomByCodeAsync(
            Context.ConnectionId, username, mobileNumber, userId, roomCode.ToUpper());

        if (!result.Success)
        {
            return new JoinRoomResponseDto { Success = false, Error = result.Error };
        }

        // Cache the player-room mapping
        await _playerRoomCache.SetPlayerRoomAsync(mobileNumber, new PlayerRoomInfo
        {
            RoomId = result.RoomId!.Value,
            RoomCode = result.RoomCode!,
            Username = username,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTime.UtcNow
        });

        await Groups.AddToGroupAsync(Context.ConnectionId, result.RoomId.Value.ToString());

        // Get room info
        var room = await _gameService.GetRoomWithPlayersAsync(result.RoomId.Value);
        var scores = await _gameService.GetScoresAsync(result.RoomId.Value);

        // Notify the joining player
        await Clients.Caller.SendAsync("JoinedRoom", new
        {
            roomId = result.RoomId,
            roomCode = result.RoomCode,
            playerId = result.PlayerId,
            isHost = result.IsHost,
            maxPlayers = room?.MaxPlayers ?? 8,
            minPlayers = room?.MinPlayers ?? 2,
            totalRounds = room?.TotalRounds ?? 3,
            roundDurationSeconds = room?.RoundDurationSeconds ?? 120
        });

        // Notify all players in the room
        if (room != null)
        {
            await Clients.Group(result.RoomId.Value.ToString()).SendAsync("RoomUpdated", new RoomDto
            {
                RoomId = room.Id,
                RoomCode = room.RoomCode,
                PlayerCount = scores.Count, // Use scores.Count for accurate player count
                MinPlayers = room.MinPlayers,
                MaxPlayers = room.MaxPlayers,
                TotalRounds = room.TotalRounds,
                RoundDurationSeconds = room.RoundDurationSeconds,
                RoomType = room.RoomType.ToString(),
                Status = room.Status.ToString(),
                CustomHintsEnabled = room.CustomHintsEnabled,
                HintLettersCount = room.HintLettersCount,
                Players = scores.Select(s => new PlayerScoreDto
                {
                    PlayerId = s.PlayerId,
                    Username = s.Username,
                    Score = s.Score,
                    IsDrawing = s.IsDrawing,
                    HasGuessedCorrectly = s.HasGuessedCorrectly,
                    IsHost = s.IsHost
                }).ToList()
            });
        }

        return new JoinRoomResponseDto
        {
            Success = true,
            RoomId = result.RoomId,
            RoomCode = result.RoomCode,
            PlayerId = result.PlayerId,
            IsHost = result.IsHost
        };
    }

    /// <summary>
    /// Host starts the game (for private rooms)
    /// </summary>
    public async Task<object> StartGameAsHost()
    {
        Console.WriteLine($"StartGameAsHost called. ConnectionId: {Context.ConnectionId}");
        
        var mobileNumber = GetMobileNumber();
        Console.WriteLine($"MobileNumber from token: {mobileNumber}");
        
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        Console.WriteLine($"Player found: {player?.Username}, IsHost: {player?.IsHost}, RoomId: {player?.RoomId}, PlayerId: {player?.Id}");
        
        if (player == null)
        {
            // Try to find player by mobile number as fallback
            Console.WriteLine($"Player not found by connection ID, trying mobile number...");
            player = await _gameService.GetPlayerByMobileNumberAsync(mobileNumber!);
            Console.WriteLine($"Player by mobile: {player?.Username}, RoomId: {player?.RoomId}");
            
            if (player != null)
            {
                // Update connection ID
                Console.WriteLine($"Updating connection ID for player {player.Id}");
                await _gameService.UpdatePlayerConnectionAsync(mobileNumber!, Context.ConnectionId);
                player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
            }
        }
        
        if (player?.RoomId == null)
        {
            Console.WriteLine("Player not in a room");
            return new { success = false, error = "Not in a room" };
        }

        if (!player.IsHost)
        {
            Console.WriteLine("Player is not the host");
            return new { success = false, error = "Only the host can start the game" };
        }

        var (canStart, errorReason) = await _gameService.CanStartGameWithReasonAsync(player.RoomId.Value);
        Console.WriteLine($"CanStart: {canStart}, Reason: {errorReason}");
        
        if (!canStart)
        {
            return new { success = false, error = errorReason ?? "Cannot start game" };
        }

        await StartGame(player.RoomId.Value);
        return new { success = true };
    }

    /// <summary>
    /// Host removes a player from the room
    /// </summary>
    public async Task<object> KickPlayer(int playerId)
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null)
        {
            return new { success = false, error = "Not in a room" };
        }

        if (!player.IsHost)
        {
            return new { success = false, error = "Only the host can kick players" };
        }

        var room = await _gameService.GetRoomWithPlayersAsync(player.RoomId.Value);
        if (room == null) return new { success = false, error = "Room not found" };

        var playerToKick = room.Players.FirstOrDefault(p => p.Id == playerId);
        if (playerToKick == null)
        {
            return new { success = false, error = "Player not found in room" };
        }

        var success = await _gameService.RemovePlayerFromRoomAsync(
            player.RoomId.Value, playerId, player.Id);

        if (!success)
        {
            return new { success = false, error = "Failed to remove player" };
        }

        // Clear kicked player's cache
        await _playerRoomCache.RemovePlayerRoomAsync(playerToKick.MobileNumber);

        // Notify kicked player
        await Clients.Client(playerToKick.ConnectionId).SendAsync("KickedFromRoom", new
        {
            message = "You have been removed from the room by the host"
        });

        // Remove from SignalR group
        await Groups.RemoveFromGroupAsync(playerToKick.ConnectionId, player.RoomId.Value.ToString());

        // Notify remaining players
        var scores = await _gameService.GetScoresAsync(player.RoomId.Value);
        await Clients.Group(player.RoomId.Value.ToString()).SendAsync("PlayerKicked", new
        {
            kickedPlayerId = playerId,
            kickedUsername = playerToKick.Username,
            players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly,
                IsHost = s.IsHost
            }).ToList()
        });

        return new { success = true };
    }

    /// <summary>
    /// Host restarts the game
    /// </summary>
    public async Task<object> RestartGame()
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null)
        {
            return new { success = false, error = "Not in a room" };
        }

        if (!player.IsHost)
        {
            return new { success = false, error = "Only the host can restart the game" };
        }

        // Stop any running timers
        lock (_timerLock)
        {
            if (_roomTimers.TryGetValue(player.RoomId.Value, out var timer))
            {
                timer.Dispose();
                _roomTimers.Remove(player.RoomId.Value);
            }
        }

        var success = await _gameService.RestartGameAsync(player.RoomId.Value, player.Id);
        if (!success)
        {
            return new { success = false, error = "Failed to restart game" };
        }

        var room = await _gameService.GetRoomWithPlayersAsync(player.RoomId.Value);
        var scores = await _gameService.GetScoresAsync(player.RoomId.Value);

        // Notify all players
        await Clients.Group(player.RoomId.Value.ToString()).SendAsync("GameRestarted", new
        {
            roomId = room?.Id,
            roomCode = room?.RoomCode,
            players = scores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly,
                IsHost = s.IsHost
            }).ToList()
        });

        return new { success = true };
    }

    // ===== FRIEND SYSTEM METHODS =====

    /// <summary>
    /// Send a friend request
    /// </summary>
    public async Task<FriendRequestResponseDto> SendFriendRequest(int targetUserId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new FriendRequestResponseDto { Success = false, Error = "Not authenticated" };
        }

        var result = await _friendService.SendFriendRequestAsync(userId.Value, targetUserId);

        if (result.Success)
        {
            // Notify target user if online
            var targetUser = await _authService.GetUserByIdAsync(targetUserId);
            var currentUser = await _authService.GetUserByIdAsync(userId.Value);
            if (targetUser?.CurrentConnectionId != null && currentUser != null)
            {
                await Clients.Client(targetUser.CurrentConnectionId).SendAsync("FriendRequestReceived", new
                {
                    friendshipId = result.FriendshipId,
                    fromUserId = userId.Value,
                    fromUsername = currentUser.Username
                });
            }
        }

        return new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            FriendshipId = result.FriendshipId
        };
    }

    /// <summary>
    /// Accept a friend request
    /// </summary>
    public async Task<FriendRequestResponseDto> AcceptFriendRequest(int friendshipId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new FriendRequestResponseDto { Success = false, Error = "Not authenticated" };
        }

        var result = await _friendService.AcceptFriendRequestAsync(friendshipId, userId.Value);

        return new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            FriendshipId = result.FriendshipId
        };
    }

    /// <summary>
    /// Decline a friend request
    /// </summary>
    public async Task<FriendRequestResponseDto> DeclineFriendRequest(int friendshipId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new FriendRequestResponseDto { Success = false, Error = "Not authenticated" };
        }

        var result = await _friendService.DeclineFriendRequestAsync(friendshipId, userId.Value);

        return new FriendRequestResponseDto
        {
            Success = result.Success,
            Error = result.Error
        };
    }

    /// <summary>
    /// Get friends list with online status
    /// </summary>
    public async Task<FriendsListDto> GetFriends()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new FriendsListDto();
        }

        var friends = await _friendService.GetFriendsAsync(userId.Value);
        var pendingRequests = await _friendService.GetPendingRequestsAsync(userId.Value);
        var sentRequests = await _friendService.GetSentRequestsAsync(userId.Value);

        return new FriendsListDto
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
        };
    }

    // ===== ROOM INVITATION METHODS =====

    /// <summary>
    /// Send a room invitation to a friend
    /// </summary>
    public async Task<InvitationResponseDto> SendRoomInvitation(int inviteeUserId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new InvitationResponseDto { Success = false, Error = "Not authenticated" };
        }

        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null)
        {
            return new InvitationResponseDto { Success = false, Error = "Not in a room" };
        }

        // Check if they are friends
        var areFriends = await _friendService.AreFriendsAsync(userId.Value, inviteeUserId);
        if (!areFriends)
        {
            return new InvitationResponseDto { Success = false, Error = "You can only invite friends" };
        }

        var result = await _invitationService.SendInvitationAsync(
            player.RoomId.Value, userId.Value, inviteeUserId);

        if (result.Success)
        {
            // Notify invitee if online
            var invitee = await _authService.GetUserByIdAsync(inviteeUserId);
            var inviter = await _authService.GetUserByIdAsync(userId.Value);
            if (invitee?.CurrentConnectionId != null && inviter != null)
            {
                await Clients.Client(invitee.CurrentConnectionId).SendAsync("RoomInvitationReceived", new RoomInvitationDto
                {
                    InvitationId = result.InvitationId!.Value,
                    RoomId = result.RoomId!.Value,
                    RoomCode = result.RoomCode!,
                    InviterId = userId.Value,
                    InviterName = inviter.Username,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(RoomInvitation.DefaultExpirySeconds),
                    SecondsRemaining = RoomInvitation.DefaultExpirySeconds
                });
            }
        }

        return new InvitationResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            InvitationId = result.InvitationId,
            RoomId = result.RoomId,
            RoomCode = result.RoomCode
        };
    }

    /// <summary>
    /// Accept a room invitation
    /// </summary>
    public async Task<InvitationResponseDto> AcceptRoomInvitation(int invitationId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new InvitationResponseDto { Success = false, Error = "Not authenticated" };
        }

        var result = await _invitationService.AcceptInvitationAsync(invitationId, userId.Value);

        if (result.Success && result.RoomCode != null)
        {
            // Join the room - the join will handle the room joining
            await JoinRoomByCode(result.RoomCode);
            
            return new InvitationResponseDto
            {
                Success = true,
                RoomId = result.RoomId,
                RoomCode = result.RoomCode
            };
        }

        return new InvitationResponseDto
        {
            Success = result.Success,
            Error = result.Error,
            RoomId = result.RoomId,
            RoomCode = result.RoomCode
        };
    }

    /// <summary>
    /// Decline a room invitation
    /// </summary>
    public async Task<InvitationResponseDto> DeclineRoomInvitation(int invitationId)
    {
        var userId = await GetCurrentUserIdAsync();
        var user = userId != null ? await _authService.GetUserByIdAsync(userId.Value) : null;
        
        if (userId == null)
        {
            return new InvitationResponseDto { Success = false, Error = "Not authenticated" };
        }

        // Get invitation details before declining
        var invitations = await _invitationService.GetPendingInvitationsAsync(userId.Value);
        var invitation = invitations.FirstOrDefault(i => i.InvitationId == invitationId);

        var result = await _invitationService.DeclineInvitationAsync(invitationId, userId.Value);

        if (result.Success && invitation != null && user != null)
        {
            // Notify the inviter that invitation was declined
            var inviter = await _authService.GetUserByIdAsync(invitation.InviterId);
            if (inviter?.CurrentConnectionId != null)
            {
                await Clients.Client(inviter.CurrentConnectionId).SendAsync("InvitationDeclined", new InvitationDeclinedNotificationDto
                {
                    InvitationId = invitationId,
                    DeclinedByUserId = userId.Value,
                    DeclinedByUsername = user.Username
                });
            }
        }

        return new InvitationResponseDto
        {
            Success = result.Success,
            Error = result.Error
        };
    }

    /// <summary>
    /// Get pending room invitations
    /// </summary>
    public async Task<List<RoomInvitationDto>> GetPendingInvitations()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return new List<RoomInvitationDto>();
        }

        var invitations = await _invitationService.GetPendingInvitationsAsync(userId.Value);
        return invitations.Select(i => new RoomInvitationDto
        {
            InvitationId = i.InvitationId,
            RoomId = i.RoomId,
            RoomCode = i.RoomCode,
            InviterId = i.InviterId,
            InviterName = i.InviterName,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            SecondsRemaining = i.SecondsRemaining
        }).ToList();
    }
}
