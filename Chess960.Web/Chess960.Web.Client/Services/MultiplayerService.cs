using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Chess960.Web.Client.Models;

namespace Chess960.Web.Services;

public class MultiplayerService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;

    public event Action<GameStartedDto>? OnGameStarted;
    public event Action<string, string, long, long>? OnMoveMade;
    public event Action? OnWaitingForMatch;
    public event Action<string, string, string, int?, int?, int?, int?>? OnGameOver; // winnerId, reason, fen, wRating, bRating, wLow, bLow
    public event Action<string>? OnDrawOffered; // senderId
    public event Action? OnDrawDeclined;
    public event Action<int, int>? OnServerStatsUpdated; // onlineUsers, gamesToday
    // requesterId, requesterName, timeControl
    public event Action<string, string, string>? OnChallengeReceived; 
    public event Action<string>? OnChallengeFailed;
    public event Action<string, string>? OnFriendRequestReceived; // requesterId, requesterName
    public event Action<string, string>? OnChatMessageReceived; // senderId, message

    public string? CurrentGameId { get; private set; }
    public string? MyConnectionId => _hubConnection?.ConnectionId;
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    public MultiplayerService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    private Task? _initTask;

    public async Task InitializeAsync()
    {
        if (_initTask != null)
        {
            await _initTask;
            return;
        }

        _initTask = ConnectAsync();
        await _initTask;
    }

    private async Task ConnectAsync()
    {
        if (_hubConnection is not null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/gamehub"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<GameStartedDto>("GameStarted", (dto) =>
        {
            CurrentGameId = dto.GameId;
            OnGameStarted?.Invoke(dto);
        });

        _hubConnection.On<string, string, long, long>("MoveMade", (move, fen, whiteTime, blackTime) =>
        {
            OnMoveMade?.Invoke(move, fen, whiteTime, blackTime);
        });

        _hubConnection.On("WaitingForMatch", () =>
        {
            OnWaitingForMatch?.Invoke();
        });

        _hubConnection.On<string, string, string, int?, int?, int?, int?>("GameOver", (winnerId, reason, fen, wNew, bNew, wDelta, bDelta) =>
        {
             OnGameOver?.Invoke(winnerId, reason, fen, wNew, bNew, wDelta, bDelta);
        });

        _hubConnection.On<string>("DrawOffered", (senderId) =>
        {
             OnDrawOffered?.Invoke(senderId);
        });

        _hubConnection.On("DrawDeclined", () =>
        {
             OnDrawDeclined?.Invoke();
        });

        _hubConnection.On<int, int>("ServerStats", (onlineUsers, gamesToday) =>
        {
             OnServerStatsUpdated?.Invoke(onlineUsers, gamesToday);
        });

        _hubConnection.On<string, string, string>("ChallengeReceived", (requesterId, requesterName, timeControl) => 
        {
             Console.WriteLine($"[MultiplayerService] ChallengeReceived from {requesterName} ({requesterId})! Invoking event...");
             if (OnChallengeReceived == null) Console.WriteLine("[MultiplayerService] WARNING: No subscribers to OnChallengeReceived!");
             OnChallengeReceived?.Invoke(requesterId, requesterName, timeControl);
        });

        _hubConnection.On<string>("ChallengeFailed", (reason) => 
        {
             OnChallengeFailed?.Invoke(reason);
        });

        _hubConnection.On<string, string>("FriendRequestReceived", (requesterId, requesterName) =>
        {
             OnFriendRequestReceived?.Invoke(requesterId, requesterName);
        });

        _hubConnection.On<string, string>("ChatMessage", (senderId, message) =>
        {
             Console.WriteLine($"[Client Service] ChatMessage received: {message} from {senderId}");
             OnChatMessageReceived?.Invoke(senderId, message);
        });

        await _hubConnection.StartAsync();
    }

    public async Task FindMatch(string timeControl, int ratingRange)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("FindMatch", UserId, timeControl, ratingRange);
        }
    }

    public async Task JoinGame(string gameId)
    {
         if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("JoinGame", gameId, UserId);
        }
    }

    public async Task MakeMove(string gameId, string move)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("MakeMove", gameId, move, UserId);
        }
    }

    public async Task ResignAsync(string gameId)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("Resign", gameId, UserId);
        }
    }

    public async Task AbortAsync(string gameId)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("Abort", gameId, UserId);
        }
    }

    public async Task OfferDrawAsync(string gameId)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("OfferDraw", gameId, UserId);
        }
    }

    public async Task RespondDrawAsync(string gameId, bool accept)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("RespondDraw", gameId, UserId, accept);
        }
    }

    public async Task SendChallengeAsync(string targetUserId, string timeControl)
    {
        if (_hubConnection is not null)
        {
             await _hubConnection.SendAsync("SendChallenge", UserId, "Me", targetUserId, timeControl); 
        }
    }

    public async Task SendChallengeAsync(string myName, string targetUserId, string timeControl)
    {
        if (_hubConnection is not null)
        {
             await _hubConnection.SendAsync("SendChallenge", UserId, myName, targetUserId, timeControl);
        }
    }

    public async Task RespondToChallengeAsync(string requesterId, bool accept, string timeControl)
    {
        if (_hubConnection is not null)
        {
             await _hubConnection.SendAsync("RespondToChallenge", requesterId, UserId, accept, timeControl);
        }
    }

    public async Task<string?> CreatePrivateGameAsync(string timeControl)
    {
        if (_hubConnection is not null)
        {
             return await _hubConnection.InvokeAsync<string>("CreatePrivateGame", UserId, timeControl);
        }
        return null;
    }

    public async Task SendMessageAsync(string gameId, string message)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("SendMessage", gameId, message, UserId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
