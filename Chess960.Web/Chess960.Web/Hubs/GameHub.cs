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
        Console.WriteLine($"[Hub] FindMatch: User={userId}, Conn={Context.ConnectionId}");
        var session = _gameManager.FindMatch(Context.ConnectionId, userId, timeControl);
        if (session != null)
        {
            Console.WriteLine($"[Hub] Match Found! Game={session.GameId}. White={session.WhiteUserId}, Black={session.BlackUserId}");
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
            Console.WriteLine($"[Hub] Added to queue: {userId}");
            // Added to queue
            await Clients.Caller.SendAsync("WaitingForMatch");
        }
    }

    public async Task<bool> JoinGame(string gameId, string userId)
    {
        Console.WriteLine($"[Hub] JoinGame: Game={gameId}, User={userId}, Conn={Context.ConnectionId}");
        var success = _gameManager.JoinGame(gameId, Context.ConnectionId, userId);
        Console.WriteLine($"[Hub] JoinGame Result: {success}");
        
        if (success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            var session = _gameManager.GetGame(gameId);
            
            // Notify both players that game started (or person reconnected)
            // Fix: Send only to caller to avoid infinite loop where opponent reloads -> joins -> triggers reload for me -> I join -> trigger reload for them...
            await Clients.Caller.SendAsync("GameStarted", 
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

    public async Task MakeMove(string gameId, string move, string userId)
    {
        Console.WriteLine($"[Hub] MakeMove: Game={gameId}, Move={move}, User={userId}, Conn={Context.ConnectionId}");
        var session = _gameManager.GetGame(gameId);
        if (session != null)
        {
            // Verify it's this player's turn
            var isWhiteTurn = session.Game.Pos.SideToMove.IsWhite;
            
            // Robust Check: Use UserID to identify player (handles reconnects/tabs/ghost connections)
            var isPlayerWhite = session.WhiteUserId == userId;
            var isPlayerBlack = session.BlackUserId == userId;
            
            Console.WriteLine($"[Hub] Turn: {(isWhiteTurn ? "White" : "Black")}. User is: {(isPlayerWhite ? "White" : (isPlayerBlack ? "Black" : "Spectator"))}");

            if ((isWhiteTurn && isPlayerWhite) || (!isWhiteTurn && isPlayerBlack))
            {
                if (_gameManager.MakeMove(gameId, move))
                {
                    Console.WriteLine($"[Hub] Move Valid. Broadcasting...");
                    // Broadcast Move
                    await Clients.Group(gameId).SendAsync("MoveMade", 
                        move, 
                        session.Game.Pos.FenNotation,
                        session.WhiteTimeRemainingMs,
                        session.BlackTimeRemainingMs);

                    // Check if game ended via this move
                    if (session.Result != GameResult.Active)
                    {
                        await Clients.Group(gameId).SendAsync("GameOver", 
                            session.WinnerUserId, 
                            session.EndReason.ToString(), 
                            session.Game.Pos.FenNotation);
                    }
                }
                else 
                {
                    Console.WriteLine($"[Hub] GameManager rejected move.");
                }
            }
            else
            {
                Console.WriteLine($"[Hub] Not player's turn or ID mismatch.");
            }
        }
        else
        {
            Console.WriteLine($"[Hub] Session not found.");
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
