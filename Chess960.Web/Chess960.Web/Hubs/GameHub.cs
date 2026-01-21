using Microsoft.AspNetCore.SignalR;
using Chess960.Web.Services;

namespace Chess960.Web.Services;

public class GameHub : Hub
{
    private readonly GameManager _gameManager;
    private readonly EloService _eloService;
    private readonly GameHistoryService _historyService;
    private readonly IConnectionTracker _connectionTracker;
    private readonly Microsoft.AspNetCore.Identity.UserManager<Chess960.Web.Data.ApplicationUser> _userManager;
    private static int _onlineUsers = 0;

    public GameHub(GameManager gameManager, EloService eloService, GameHistoryService historyService, IConnectionTracker connectionTracker, Microsoft.AspNetCore.Identity.UserManager<Chess960.Web.Data.ApplicationUser> userManager)
    {
        _gameManager = gameManager;
        _eloService = eloService;
        _historyService = historyService;
        _connectionTracker = connectionTracker;
        _userManager = userManager;
    }

    // ... OnConnected/Disconnected unchanged ...

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

    public async Task FindMatch(string userId, string timeControl, int ratingRange)
    {
        Console.WriteLine($"[Hub] FindMatch: User={userId}, Conn={Context.ConnectionId}, Range={ratingRange}");
        
        // Fetch Current Rating for this Time Control
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        int currentRating = 1200; // Default
        if (timeControl.Contains("1+") || timeControl.Contains("2+"))
            currentRating = user.EloBullet;
        else if (timeControl.Contains("3+") || timeControl.Contains("5+"))
            currentRating = user.EloBlitz;
        else
            currentRating = user.EloRapid;

        var session = _gameManager.FindMatch(Context.ConnectionId, userId, timeControl, currentRating, ratingRange);
        
        if (session != null)
        {
            Console.WriteLine($"[Hub] Match Found! Game={session.GameId}. White={session.WhiteUserId}, Black={session.BlackUserId}");
            // Match found!
            await Groups.AddToGroupAsync(session.WhitePlayerId, session.GameId);
            await Groups.AddToGroupAsync(session.BlackPlayerId!, session.GameId);
            
            var (whiteRating, blackRating) = await _eloService.GetRatingsAsync(session.WhiteUserId, session.BlackUserId, session.TimeControl);

            // Fetch Avatars
            var whiteUser = await _userManager.FindByIdAsync(session.WhiteUserId);
            var blackUser = await _userManager.FindByIdAsync(session.BlackUserId ?? "");
            string? whiteAvatar = whiteUser?.ProfilePictureUrl;
            string? blackAvatar = blackUser?.ProfilePictureUrl;

            // Notify both players
            await Clients.Group(session.GameId).SendAsync("GameStarted", new Chess960.Web.Client.Models.GameStartedDto
            {
                GameId = session.GameId,
                Fen = session.Game.Pos.FenNotation,
                WhiteId = session.WhiteUserId,
                BlackId = session.BlackUserId ?? "",
                WhiteTimeMs = session.WhiteTimeRemainingMs,
                BlackTimeMs = session.BlackTimeRemainingMs,
                WhiteRating = whiteRating,
                BlackRating = blackRating,
                WhiteAvatar = whiteAvatar,
                BlackAvatar = blackAvatar,
                WhiteName = whiteUser?.UserName ?? "White",
                BlackName = blackUser?.UserName ?? "Black"
            });
        }
        else
        {
             Console.WriteLine($"[Hub] Added to queue: {userId} ({currentRating} +/- {ratingRange})");
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
            
            var whiteUser = await _userManager.FindByIdAsync(session.WhiteUserId);
            var blackUser = await _userManager.FindByIdAsync(session.BlackUserId ?? "");
            string? whiteAvatar = whiteUser?.ProfilePictureUrl;
            string? blackAvatar = blackUser?.ProfilePictureUrl;

            // Notify both players that game started (or person reconnected)
            await Clients.Group(gameId).SendAsync("GameStarted", new Chess960.Web.Client.Models.GameStartedDto
            {
                GameId = session?.GameId ?? "",
                Fen = session?.Game.Pos.FenNotation ?? "",
                WhiteId = session?.WhiteUserId ?? "",
                BlackId = session?.BlackUserId ?? "",
                WhiteTimeMs = session?.WhiteTimeRemainingMs ?? 0,
                BlackTimeMs = session?.BlackTimeRemainingMs ?? 0,
                WhiteRating = whiteRating,
                BlackRating = blackRating,
                WhiteAvatar = whiteAvatar,
                BlackAvatar = blackAvatar,
                WhiteName = whiteUser?.UserName ?? "White",
                BlackName = blackUser?.UserName ?? "Black"
            });
            return true;
        }
        return false;
    }

    public async Task MakeMove(string gameId, string move, string userId)
    {
        Console.WriteLine($"[Hub] MakeMove: Game={gameId}, Move={move}. Caller={Context.UserIdentifier}, ParamUser={userId}");
        
        // Quick check if game exists
        var session = _gameManager.GetGame(gameId);
        if (session == null) 
        {
             Console.WriteLine("[Hub] Game not found.");
             return;
        }

        Console.WriteLine($"[Hub] Game White={session.WhiteUserId}, Black={session.BlackUserId}");

        // Security Check: Is it this user's turn?
        // Fallback: Use ParamUser if Context.UserIdentifier is missing (for debugging/resilience)
        var effectiveUser = !string.IsNullOrEmpty(Context.UserIdentifier) ? Context.UserIdentifier : userId;

        if (session.WhiteUserId != effectiveUser && session.BlackUserId != effectiveUser)
        {
             Console.WriteLine($"[Hub] Auth Failed. Caller {effectiveUser} is not White or Black.");
             return;
        }

        // Make Move
        var moveResult = _gameManager.MakeMove(gameId, move);
        Console.WriteLine($"[Hub] GameManager result: {moveResult.Success}. Fen: {moveResult.Fen}");
        
        if (moveResult.Success)
        {
            // Broadcast move to group
            await Clients.Group(gameId).SendAsync("MoveMade", move, moveResult.Fen, moveResult.WhiteTimeMs, moveResult.BlackTimeMs);
            Console.WriteLine("[Hub] Broadcast sent.");

            // Check Game Over
            if (moveResult.Result != GameResult.Active)
            {
               await HandleGameOver(session, moveResult);
            }
        }
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

        // Fetch Avatars
        var whiteUser = await _userManager.FindByIdAsync(whiteId);
        var blackUser = await _userManager.FindByIdAsync(blackId);
        string? whiteAvatar = whiteUser?.ProfilePictureUrl;
        string? blackAvatar = blackUser?.ProfilePictureUrl;

        // Notify both players directly (safer than Group for immediate start)
        await Clients.Clients(whiteConn, blackConn).SendAsync("GameStarted", new Chess960.Web.Client.Models.GameStartedDto
        {
            GameId = session.GameId,
            Fen = session.Game.Pos.FenNotation,
            WhiteId = session.WhiteUserId,
            BlackId = session.BlackUserId ?? "",
            WhiteTimeMs = session.WhiteTimeRemainingMs,
            BlackTimeMs = session.BlackTimeRemainingMs,
            WhiteRating = whiteRating,
            BlackRating = blackRating,
            WhiteAvatar = whiteAvatar,
            BlackAvatar = blackAvatar,
            WhiteName = whiteUser?.UserName ?? "White",
            BlackName = blackUser?.UserName ?? "Black"
        });
    }

    public async Task<string> CreatePrivateGame(string userId, string timeControl)
    {
         // Create a game with NO black player yet
         var session = _gameManager.CreateGame(Context.ConnectionId, userId, "", "", timeControl);
         
         // Add creator to group
         await Groups.AddToGroupAsync(Context.ConnectionId, session.GameId);
         
         return session.GameId;
    }

    public async Task SendMessage(string gameId, string message, string userId)
    {
        Console.WriteLine($"[Hub] SendMessage: Game={gameId}, Msg={message}, User={userId}");
        // Broadcast to group
        await Clients.Group(gameId).SendAsync("ChatMessage", userId, message);
    }

    public async Task Resign(string gameId, string userId)
    {
         if (userId != Context.UserIdentifier) return;
         
         Console.WriteLine($"[Hub] Resign: Game={gameId}, User={userId}");
         var session = _gameManager.Resign(gameId, userId);
         if (session != null)
         {
             var result = new MoveResult(true, session.Game.Pos.FenNotation, session.WhiteTimeRemainingMs, session.BlackTimeRemainingMs, session.Result, session.EndReason, session.WinnerUserId);
             await HandleGameOver(session, result);
         }
    }

    public async Task Abort(string gameId, string userId)
    {
         if (userId != Context.UserIdentifier) return;

         Console.WriteLine($"[Hub] Abort: Game={gameId}, User={userId}");
         var session = _gameManager.Abort(gameId, userId);
         if (session != null)
         {
             var result = new MoveResult(true, session.Game.Pos.FenNotation, session.WhiteTimeRemainingMs, session.BlackTimeRemainingMs, session.Result, session.EndReason, session.WinnerUserId);
             await HandleGameOver(session, result);
         }
    }

    public async Task OfferDraw(string gameId, string userId)
    {
         if (userId != Context.UserIdentifier) return;

         Console.WriteLine($"[Hub] OfferDraw: Game={gameId}, User={userId}");
         var success = _gameManager.OfferDraw(gameId, userId);
         if (success)
         {
             await Clients.Group(gameId).SendAsync("DrawOffered", userId);
         }
    }

    public async Task RespondDraw(string gameId, string userId, bool accept)
    {
         if (userId != Context.UserIdentifier) return;

         Console.WriteLine($"[Hub] RespondDraw: Game={gameId}, User={userId}, Accept={accept}");
         var session = _gameManager.RespondDraw(gameId, userId, accept);
         
         if (!accept)
         {
             await Clients.Group(gameId).SendAsync("DrawDeclined");
         }
         else if (session != null)
         {
             // Draw Accepted -> Game Over
             var result = new MoveResult(true, session.Game.Pos.FenNotation, session.WhiteTimeRemainingMs, session.BlackTimeRemainingMs, session.Result, session.EndReason, session.WinnerUserId);
             await HandleGameOver(session, result);
         }
    }

    private async Task HandleGameOver(GameSession session, MoveResult moveResult)
    {
        // Calculate ELO changes (Skip if Aborted)
        int wNew = 0, bNew = 0, wDelta = 0, bDelta = 0;

        if (moveResult.Result != GameResult.Aborted)
        {
            var result = await _eloService.UpdateRatingsAsync(session.WhiteUserId, session.BlackUserId, moveResult.Result, session.TimeControl);
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
        await _historyService.SaveGameAsync(session, moveResult.Result, moveResult.EndReason.ToString());

        await BroadcastStats(); // Update game count

        await Clients.Group(session.GameId).SendAsync("GameOver", 
             moveResult.WinnerId, 
             moveResult.EndReason.ToString(), 
             moveResult.Fen,
             wNew, bNew, wDelta, bDelta);
    }
}
