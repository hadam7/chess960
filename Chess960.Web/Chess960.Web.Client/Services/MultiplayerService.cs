using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;

namespace Chess960.Web.Services;

public class MultiplayerService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;

    public event Action<string, string, string, string, long, long, int, int>? OnGameStarted; // ..., whiteRating, blackRating
    public event Action<string, string, long, long>? OnMoveMade;
    public event Action? OnWaitingForMatch;
    public event Action<string, string, string, int?, int?, int?, int?>? OnGameOver; // winnerId, reason, fen, wRating, bRating, wLow, bLow
    public event Action<string>? OnDrawOffered; // senderId
    public event Action? OnDrawDeclined;
    public event Action<int, int>? OnServerStatsUpdated; // onlineUsers, gamesToday

    public string? CurrentGameId { get; private set; }
    public string? MyConnectionId => _hubConnection?.ConnectionId;
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    public MultiplayerService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public async Task InitializeAsync()
    {
        if (_hubConnection is not null) return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/gamehub"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string, string, string, string, long, long, int, int>("GameStarted", (gameId, fen, whiteId, blackId, whiteTime, blackTime, wRating, bRating) =>
        {
            CurrentGameId = gameId;
            OnGameStarted?.Invoke(gameId, fen, whiteId, blackId, whiteTime, blackTime, wRating, bRating);
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

        await _hubConnection.StartAsync();
    }

    public async Task FindMatch(string timeControl)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("FindMatch", UserId, timeControl);
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

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
