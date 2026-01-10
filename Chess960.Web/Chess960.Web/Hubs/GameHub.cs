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

    public async Task FindMatch(string userId, string timeControl)
    {
        var session = _gameManager.FindMatch(Context.ConnectionId, userId, timeControl);
        if (session != null)
        {
            // Match found!
            await Groups.AddToGroupAsync(session.WhitePlayerId, session.GameId);
            await Groups.AddToGroupAsync(session.BlackPlayerId!, session.GameId);
            
            // Notify both players
            await Clients.Group(session.GameId).SendAsync("GameStarted", 
                session.GameId, 
                session.Game.Pos.FenNotation, 
                session.WhiteUserId, 
                session.BlackUserId,
                session.WhiteTimeRemainingMs,
                session.BlackTimeRemainingMs);
        }
        else
        {
            // Added to queue
            await Clients.Caller.SendAsync("WaitingForMatch");
        }
    }

    public async Task<bool> JoinGame(string gameId, string userId)
    {
        var success = _gameManager.JoinGame(gameId, Context.ConnectionId, userId);
        if (success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            var session = _gameManager.GetGame(gameId);
            
            // Notify both players that game started (or person reconnected)
            await Clients.Group(gameId).SendAsync("GameStarted", 
                session?.GameId, 
                session?.Game.Pos.FenNotation, 
                session?.WhiteUserId, 
                session?.BlackUserId,
                session?.WhiteTimeRemainingMs,
                session?.BlackTimeRemainingMs);
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
                    // Broadcast Move
                    await Clients.Group(gameId).SendAsync("MoveMade", 
                        move, 
                        session.Game.Pos.FenNotation,
                        session.WhiteTimeRemainingMs,
                        session.BlackTimeRemainingMs);

                    // Check if game ended via this move (Checkmate/Stalemate/Timeout detected in GameManager)
                    if (session.Result != GameResult.Active)
                    {
                        await Clients.Group(gameId).SendAsync("GameOver", 
                            session.WinnerUserId, 
                            session.EndReason.ToString(), 
                            session.Game.Pos.FenNotation);
                    }
                }
            }
        }
    }

    public async Task Resign(string gameId)
    {
         var session = _gameManager.GetGame(gameId);
         if (session == null) return;
         
         var userId = session.WhitePlayerId == Context.ConnectionId ? session.WhiteUserId : session.BlackUserId;
         if (userId == null) return;

         // Try to resign
         var endedSession = _gameManager.Resign(gameId, userId);
         
         if (endedSession != null)
         {
             await Clients.Group(gameId).SendAsync("GameOver", 
                 endedSession.WinnerUserId, 
                 endedSession.EndReason.ToString(), 
                 endedSession.Game.Pos.FenNotation);
         }
    }

    public async Task OfferDraw(string gameId)
    {
        var session = _gameManager.GetGame(gameId);
        if (session == null) return;
        
        var userId = session.WhitePlayerId == Context.ConnectionId ? session.WhiteUserId : session.BlackUserId;
        if (userId == null) return;

        if (_gameManager.OfferDraw(gameId, userId))
        {
            await Clients.Group(gameId).SendAsync("DrawOffered", userId);
        }
    }

    public async Task RespondDraw(string gameId, bool accept)
    {
        var session = _gameManager.GetGame(gameId);
        if (session == null) return;
        
        var userId = session.WhitePlayerId == Context.ConnectionId ? session.WhiteUserId : session.BlackUserId;
        if (userId == null) return;

        var endedSession = _gameManager.RespondDraw(gameId, userId, accept);
        
        if (endedSession != null)
        {
            // Draw Accepted -> Game Over
             await Clients.Group(gameId).SendAsync("GameOver", 
                 endedSession.WinnerUserId, 
                 endedSession.EndReason.ToString(), 
                 endedSession.Game.Pos.FenNotation);
        }
        else if (!accept)
        {
            // Draw Declined
             await Clients.Group(gameId).SendAsync("DrawDeclined");
        }
    }
}
