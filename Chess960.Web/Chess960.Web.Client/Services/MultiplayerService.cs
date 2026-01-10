using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;

namespace Chess960.Web.Client.Services;

public class MultiplayerService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;

    public event Action<string, string, string, string, long, long>? OnGameStarted;
    public event Action<string, string, long, long>? OnMoveMade;
    public event Action? OnWaitingForMatch;
    public event Action<string, string, string>? OnGameOver; // winnerId, reason, fen
    public event Action<string>? OnDrawOffered; // senderId
    public event Action? OnDrawDeclined;

    public string? CurrentGameId { get; private set; }
    public string? MyConnectionId => _hubConnection?.ConnectionId;
    public string UserId { get; private set; } = Guid.NewGuid().ToString();

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

        _hubConnection.On<string, string, string, string, long, long>("GameStarted", (gameId, fen, whiteId, blackId, whiteTime, blackTime) =>
        {
            CurrentGameId = gameId;
            OnGameStarted?.Invoke(gameId, fen, whiteId, blackId, whiteTime, blackTime);
        });

        _hubConnection.On<string, string, long, long>("MoveMade", (move, fen, whiteTime, blackTime) =>
        {
            OnMoveMade?.Invoke(move, fen, whiteTime, blackTime);
        });

        _hubConnection.On("WaitingForMatch", () =>
        {
            OnWaitingForMatch?.Invoke();
        });

        _hubConnection.On<string, string, string>("GameOver", (winnerId, reason, fen) =>
        {
             OnGameOver?.Invoke(winnerId, reason, fen);
        });

        _hubConnection.On<string>("DrawOffered", (senderId) =>
        {
             OnDrawOffered?.Invoke(senderId);
        });

        _hubConnection.On("DrawDeclined", () =>
        {
             OnDrawDeclined?.Invoke();
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
            await _hubConnection.SendAsync("MakeMove", gameId, move);
        }
    }

    public async Task ResignAsync(string gameId)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("Resign", gameId);
        }
    }

    public async Task OfferDrawAsync(string gameId)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("OfferDraw", gameId);
        }
    }

    public async Task RespondDrawAsync(string gameId, bool accept)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("RespondDraw", gameId, accept);
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
