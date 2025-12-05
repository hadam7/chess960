using Microsoft.AspNetCore.SignalR;
using Chess960.Web.Services;

namespace Chess960.Web.Hubs;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;

    public GameHub(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public async Task<string> CreateGame(string? fen = null)
    {
        var session = _gameManager.CreateGame(Context.ConnectionId, fen);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.GameId);
        return session.GameId;
    }

    public async Task<bool> JoinGame(string gameId)
    {
        var success = _gameManager.JoinGame(gameId, Context.ConnectionId);
        if (success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            var session = _gameManager.GetGame(gameId);
            
            // Notify both players that game started
            await Clients.Group(gameId).SendAsync("GameStarted", session?.Game.Pos.FenNotation, session?.WhitePlayerId, session?.BlackPlayerId);
            return true;
        }
        return false;
    }

    public async Task MakeMove(string gameId, string move)
    {
        var session = _gameManager.GetGame(gameId);
        if (session != null)
        {
            // Verify it's this player's turn
            var isWhiteTurn = session.Game.Pos.SideToMove.IsWhite;
            var isPlayerWhite = Context.ConnectionId == session.WhitePlayerId;
            
            if (isWhiteTurn == isPlayerWhite)
            {
                if (_gameManager.MakeMove(gameId, move))
                {
                    await Clients.Group(gameId).SendAsync("MoveMade", move, session.Game.Pos.FenNotation);
                }
            }
        }
    }
}
