using Microsoft.AspNetCore.SignalR;
using Chess960.Web.Services;

namespace Chess960.Web.Services;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;
    private readonly EloService _eloService;
    private readonly GameHistoryService _historyService;
    private readonly IConnectionTracker _connectionTracker;
    private static int _onlineUsers = 0;

    public GameHub(GameManager gameManager, EloService eloService, GameHistoryService historyService, IConnectionTracker connectionTracker)
    {
        _gameManager = gameManager;
        _eloService = eloService;
        _historyService = historyService;
        _connectionTracker = connectionTracker;
    }

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _onlineUsers);
        
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            _connectionTracker.UserConnected(userId);
            _gameManager.RegisterUser(userId, Context.ConnectionId);
        }

        await BroadcastStats();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _onlineUsers);
        
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            _connectionTracker.UserDisconnected(userId);
            _gameManager.UnregisterUser(userId);
        }

        await BroadcastStats();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastStats()
    {
        var gamesToday = await _historyService.GetGamesPlayedTodayAsync();
        await Clients.All.SendAsync("ServerStats", _onlineUsers, gamesToday);
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
            
            var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId, session.TimeControl);

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
            
            var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId, session.TimeControl);

            // Notify both players that game started (or person reconnected)
            // Use Group so both hear it (essential for Private Game join)
            await Clients.Group(gameId).SendAsync("GameStarted", 
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

    public async Task Abort(string gameId, string userId)
    {
         var session = _gameManager.GetGame(gameId);
         if (session == null) return;
         
         var endedSession = _gameManager.Abort(gameId, userId);
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

    public async Task SendChallenge(string requesterId, string requesterName, string targetUserId, string timeControl)
    {
         Console.WriteLine($"[Hub] SendChallenge: From={requesterName} ({requesterId}) To={targetUserId}");
         
         var targetConnId = _gameManager.GetConnectionId(targetUserId);
         
         if (string.IsNullOrEmpty(targetConnId))
         {
             Console.WriteLine($"[Hub] WARNING: Target user {targetUserId} not found in GameManager connections.");
             // Notify caller that target is offline/not found
             await Clients.Caller.SendAsync("ChallengeFailed", "User seems offline (no connection found).");
             return;
         }

         Console.WriteLine($"[Hub] Sending ChallengeReceived to {targetConnId}");
         await Clients.Client(targetConnId).SendAsync("ChallengeReceived", requesterId, requesterName, timeControl);
    }

    public async Task RespondToChallenge(string requesterId, string targetUserId, bool accept, string timeControl)
    {
        if (!accept) return;

        // Create Game
        var requesterConnId = _gameManager.GetConnectionId(requesterId);
        var targetConnId = Context.ConnectionId; // _gameManager.GetConnectionId(targetUserId);

        if (string.IsNullOrEmpty(requesterConnId)) 
        {
            // Requester went offline?
             await Clients.Caller.SendAsync("ChallengeFailed", "Requester is offline.");
             return;
        }

        // Randomize colors or fixed? Let's random
        string whiteId, blackId, whiteConn, blackConn;
        if (Random.Shared.Next(2) == 0)
        {
            whiteId = requesterId; whiteConn = requesterConnId;
            blackId = targetUserId; blackConn = targetConnId;
        }
        else 
        {
            whiteId = targetUserId; whiteConn = targetConnId;
            blackId = requesterId; blackConn = requesterConnId;
        }

        var session = _gameManager.CreateGame(whiteConn, whiteId, blackConn, blackId, timeControl);

        await Groups.AddToGroupAsync(whiteConn, session.GameId);
        await Groups.AddToGroupAsync(blackConn, session.GameId);

        var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(whiteId, blackId, timeControl);

        // Notify both players directly (safer than Group for immediate start)
        await Clients.Clients(whiteConn, blackConn).SendAsync("GameStarted", 
                session.GameId, 
                session.Game.Pos.FenNotation, 
                session.WhiteUserId, 
                session.BlackUserId,
                session.WhiteTimeRemainingMs,
                session.BlackTimeRemainingMs,
                whiteRating,
                blackRating);
    }

    public async Task<string> CreatePrivateGame(string userId, string timeControl)
    {
         // Create a game with NO black player yet
         var session = _gameManager.CreateGame(Context.ConnectionId, userId, "", "", timeControl);
         
         // Add creator to group
         await Groups.AddToGroupAsync(Context.ConnectionId, session.GameId);
         
         return session.GameId;
    }

    private async Task HandleGameOver(GameSession session)
    {
        // Calculate ELO changes (Skip if Aborted)
        int wNew = 0, bNew = 0, wDelta = 0, bDelta = 0;

        if (session.Result != GameResult.Aborted)
        {
            var result = await _eloService.UpdateRatingsAsync(session.WhiteUserId, session.BlackUserId, session.Result, session.TimeControl);
            wNew = result.whiteNew;
            bNew = result.blackNew; 
            wDelta = result.whiteDelta;
            bDelta = result.blackDelta;
        }
        else 
        {
             // Fetch current ratings without changing them
             var ratings = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId, session.TimeControl);
             wNew = ratings.whiteRating;
             bNew = ratings.blackRating;
        }

        // Save Game History
        await _historyService.SaveGameAsync(session, session.Result, session.EndReason.ToString());

        await BroadcastStats(); // Update game count

        await Clients.Group(session.GameId).SendAsync("GameOver", 
             session.WinnerUserId, 
             session.EndReason.ToString(), 
             session.Game.Pos.FenNotation,
             wNew, bNew, wDelta, bDelta);
    }
}
