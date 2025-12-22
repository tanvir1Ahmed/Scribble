using Microsoft.AspNetCore.SignalR;
using Scribble.Business.Interfaces;
using Scribble.Repository.Data.Entities;
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
    private static readonly Dictionary<int, Timer> _roomTimers = new();
    private static readonly object _timerLock = new();

    public GameHub(
        IGameService gameService, 
        ILeaderboardService leaderboardService, 
        IServiceScopeFactory serviceScopeFactory, 
        IHubContext<GameHub> hubContext,
        IPlayerRoomCacheService playerRoomCache)
    {
        _gameService = gameService;
        _leaderboardService = leaderboardService;
        _serviceScopeFactory = serviceScopeFactory;
        _hubContext = hubContext;
        _playerRoomCache = playerRoomCache;
    }

    private string? GetMobileNumber()
    {
        return Context.User?.FindFirst(ClaimTypes.MobilePhone)?.Value 
            ?? Context.User?.FindFirst("mobile_number")?.Value;
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
            MaxPlayers = Room.MaxPlayers,
            Status = updatedRoom?.Status.ToString() ?? "Unknown",
            Players = updatedScores.Select(s => new PlayerScoreDto
            {
                PlayerId = s.PlayerId,
                Username = s.Username,
                Score = s.Score,
                IsDrawing = s.IsDrawing,
                HasGuessedCorrectly = s.HasGuessedCorrectly
            }).ToList()
        });

        // Check if room is full and start the game
        if (updatedRoom?.Players.Count >= Room.MaxPlayers)
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
        var success = await _gameService.StartGameAsync(roomId);
        if (!success) return;

        var room = await _gameService.GetRoomWithPlayersAsync(roomId);
        if (room == null) return;

        var drawer = room.Players.FirstOrDefault(p => p.IsDrawing);
        var wordOptions = await _gameService.GetWordOptionsAsync(roomId);
        var scores = await _gameService.GetScoresAsync(roomId);

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

        // Send word options only to the drawer
        if (drawer != null)
        {
            await Clients.Client(drawer.ConnectionId).SendAsync("SelectWord", wordOptions);
        }
    }

    public async Task WordSelected(string word)
    {
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);
        if (player?.RoomId == null || !player.IsDrawing) return;

        await _gameService.SelectWordAsync(player.RoomId.Value, word);

        var room = await _gameService.GetRoomByIdAsync(player.RoomId.Value);

        // Create a hint (show first letter and underscores)
        var hint = string.Join(" ", word.ToLower().Select((c, i) => i == 0 ? c.ToString() : "_"));

        // Notify all players that drawing has started
        await Clients.Group(player.RoomId.Value.ToString()).SendAsync("DrawingStarted", new DrawingStartedDto
        {
            Hint = hint,
            WordLength = word.Length,
            Duration = Room.RoundDurationSeconds
        });

        // Start the timer
        StartRoundTimer(player.RoomId.Value);
    }

    private void StartRoundTimer(int roomId)
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
            }, null, Room.RoundDurationSeconds * 1000, Timeout.Infinite);

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
        var player = await _gameService.GetPlayerByConnectionIdAsync(Context.ConnectionId);

        if (player?.RoomId != null)
        {
            var roomId = player.RoomId.Value;
            var wasDrawing = player.IsDrawing;
            var playerUsername = player.Username;
            var mobileNumber = player.MobileNumber;

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
                        HasGuessedCorrectly = s.HasGuessedCorrectly
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
}
