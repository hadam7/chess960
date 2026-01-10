using Microsoft.AspNetCore.SignalR;
using Chess960.Web.Services;

namespace Chess960.Web.Hubs;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;
    private readonly EloService _eloService;
    private readonly GameHistoryService _historyService;

    public GameHub(GameManager gameManager, EloService eloService, GameHistoryService historyService)
    {
        _gameManager = gameManager;
        _eloService = eloService;
        _historyService = historyService;
    }

    // ... FindMatch and JoinGame unchanged ...

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
            
            var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId);

            // Notify both players
            await Clients.Group(session.GameId).SendAsync("GameStarted", 
                session.GameId, 
                session.Game.Pos.FenNotation, 
                session.WhiteUserId, 
                session.BlackUserId,
                session.WhiteTimeRemainingMs,
                session.BlackTimeRemainingMs,
                whiteRating,
                blackRating);
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
            
            var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId);

            // Notify both players that game started (or person reconnected)
            await Clients.Caller.SendAsync("GameStarted", 
                session?.GameId, 
                session?.Game.Pos.FenNotation, 
                session?.WhiteUserId, 
                session?.BlackUserId,
                session?.WhiteTimeRemainingMs,
                session?.BlackTimeRemainingMs,
                whiteRating,
                blackRating);
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
                        await HandleGameOver(session);
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

    public async Task Resign(string gameId, string userId)
    {
         var session = _gameManager.GetGame(gameId);
         if (session == null) return;
         
         // Try to resign using actual UserId
         var endedSession = _gameManager.Resign(gameId, userId);
         
         if (endedSession != null)
         {
             await HandleGameOver(endedSession);
         }
    }

    public async Task OfferDraw(string gameId, string userId)
    {
        var session = _gameManager.GetGame(gameId);
        if (session == null) return;

        if (_gameManager.OfferDraw(gameId, userId))
        {
            await Clients.Group(gameId).SendAsync("DrawOffered", userId);
        }
    }

    public async Task RespondDraw(string gameId, string userId, bool accept)
    {
        var session = _gameManager.GetGame(gameId);
        if (session == null) return;

        var endedSession = _gameManager.RespondDraw(gameId, userId, accept);
        
        if (endedSession != null)
        {
            // Draw Accepted -> Game Over
             await HandleGameOver(endedSession);
        }
        else if (!accept)
        {
            // Draw Declined
             await Clients.Group(gameId).SendAsync("DrawDeclined");
        }
    }

    private async Task HandleGameOver(GameSession session)
    {
        // Calculate ELO changes
        var (wNew, bNew, wDelta, bDelta) = await _eloService.UpdateRatingsAsync(session.WhiteUserId, session.BlackUserId, session.Result);

        // Save Game History
        await _historyService.SaveGameAsync(session, session.Result, session.EndReason.ToString());

        await Clients.Group(session.GameId).SendAsync("GameOver", 
             session.WinnerUserId, 
             session.EndReason.ToString(), 
             session.Game.Pos.FenNotation,
             wNew, bNew, wDelta, bDelta);
    }
}
