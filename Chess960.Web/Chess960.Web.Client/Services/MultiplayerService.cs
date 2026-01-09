using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;

namespace Chess960.Web.Client.Services;

public class MultiplayerService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;

    public event Action<string, string, string, string>? OnGameStarted;
    public event Action<string, string>? OnMoveMade;
    public event Action? OnWaitingForMatch;

    public string? CurrentGameId { get; private set; }
    public string? MyConnectionId => _hubConnection?.ConnectionId;

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

        _hubConnection.On<string, string, string, string>("GameStarted", (gameId, fen, whiteId, blackId) =>
        {
            CurrentGameId = gameId;
            OnGameStarted?.Invoke(gameId, fen, whiteId, blackId);
        });

        _hubConnection.On<string, string>("MoveMade", (move, fen) =>
        {
            OnMoveMade?.Invoke(move, fen);
        });

        _hubConnection.On("WaitingForMatch", () =>
        {
            OnWaitingForMatch?.Invoke();
        });

        await _hubConnection.StartAsync();
    }

    public async Task FindMatch()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("FindMatch");
        }
    }

    public async Task MakeMove(string gameId, string move)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.SendAsync("MakeMove", gameId, move);
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
