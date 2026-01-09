using System.Collections.Concurrent;
using Rudzoft.ChessLib;
using Rudzoft.ChessLib.Factories;
using Rudzoft.ChessLib.Types;
using Rudzoft.ChessLib.MoveGeneration;

namespace Chess960.Web.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, GameSession> _games = new();
    // Key: TimeControl (e.g., "3+2"), Value: Queue of waiting players
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(string ConnectionId, string UserId)>> _waitingQueues = new();

    public GameSession? FindMatch(string playerConnectionId, string userId, string timeControl)
    {
        var queue = _waitingQueues.GetOrAdd(timeControl, _ => new ConcurrentQueue<(string ConnectionId, string UserId)>());

        if (queue.TryDequeue(out var opponent))
        {
            // Prevent matching with self if clicked twice quickly
            if (opponent.UserId == userId)
            {
                queue.Enqueue(opponent);
                return null;
            }
            
            return CreateGame(opponent.ConnectionId, opponent.UserId, playerConnectionId, userId, timeControl);
        }
        
        queue.Enqueue((playerConnectionId, userId));
        return null;
    }

    public GameSession CreateGame(string whiteConnectionId, string whiteUserId, string blackConnectionId, string blackUserId, string timeControlIn)
    {
        var gameId = GenerateGameId();
        var game = GameFactory.Create();
        game.NewGame();

        // Parse Time Control (Format: "minutes+increment" e.g., "3+2")
        var parts = timeControlIn.Split('+');
        int minutes = int.Parse(parts[0]);
        int increment = int.Parse(parts[1]);
        long totalMilliseconds = minutes * 60 * 1000;

        var session = new GameSession
        {
            GameId = gameId,
            HostConnectionId = whiteConnectionId,
            Game = game,
            WhitePlayerId = whiteConnectionId,
            WhiteUserId = whiteUserId,
            BlackPlayerId = blackConnectionId,
            BlackUserId = blackUserId,
            TimeControl = timeControlIn,
            WhiteTimeRemainingMs = totalMilliseconds,
            BlackTimeRemainingMs = totalMilliseconds,
            IncrementMs = increment * 1000,
            LastMoveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _games.TryAdd(gameId, session);
        return session;
    }

    public GameSession? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var session);
        return session;
    }

    public bool JoinGame(string gameId, string playerConnectionId, string userId)
    {
        if (_games.TryGetValue(gameId, out var session))
        {
            // Reconnect logic
            if (session.WhiteUserId == userId)
            {
                session.WhitePlayerId = playerConnectionId; 
                return true;
            }
            if (session.BlackUserId == userId)
            {
                session.BlackPlayerId = playerConnectionId; 
                return true;
            }

            // New Join Logic (as Black) - Should simpler logic exist here if created via matching?
            if (string.IsNullOrEmpty(session.BlackPlayerId) && string.IsNullOrEmpty(session.BlackUserId))
            {
                session.BlackPlayerId = playerConnectionId;
                session.BlackUserId = userId;
                return true;
            }
        }
        return false;
    }

    public bool MakeMove(string gameId, string moveString)
    {
        if (_games.TryGetValue(gameId, out var session))
        {
            try 
            {
                var from = new Square(moveString.Substring(0, 2));
                var to = new Square(moveString.Substring(2, 2));
                
                var moveList = session.Game.Pos.GenerateMoves();
                var move = moveList.FirstOrDefault(m => m.Move.FromSquare() == from && m.Move.ToSquare() == to);
                
                if (!move.Move.Equals(default(Move)))
                {
                    // Calculate and deduct time
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var elapsed = now - session.LastMoveTimestamp;
                    
                    // Whose turn WAS it? (Who just made the move)
                    // If SideToMove IS White, it means White hasn't moved yet? No, updates happen after MakeMove usually.
                    // Rudzoft MakeMove updates the state. So we check BEFORE the move.
                    
                    bool isWhiteMove = session.Game.Pos.SideToMove.IsWhite;
                    
                    if (isWhiteMove)
                    {
                        session.WhiteTimeRemainingMs -= elapsed;
                        if (session.WhiteTimeRemainingMs < 0) return false; // Timeout? Handle properly (flag fall)
                        session.WhiteTimeRemainingMs += session.IncrementMs;
                    }
                    else
                    {
                        session.BlackTimeRemainingMs -= elapsed;
                        if (session.BlackTimeRemainingMs < 0) return false; 
                        session.BlackTimeRemainingMs += session.IncrementMs;
                    }
                    
                    session.LastMoveTimestamp = now;

                    session.Game.Pos.MakeMove(move.Move, session.Game.Pos.State);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public void RemoveGame(string gameId)
    {
        _games.TryRemove(gameId, out _);
    }

    private string GenerateGameId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class GameSession
{
    public string GameId { get; set; } = "";
    public string HostConnectionId { get; set; } = "";
    public string WhitePlayerId { get; set; } = "";
    public string WhiteUserId { get; set; } = "";
    public string? BlackPlayerId { get; set; }
    public string? BlackUserId { get; set; }
    public IGame Game { get; set; } = default!;
    
    // Time Control
    public string TimeControl { get; set; } = "10+0";
    public long WhiteTimeRemainingMs { get; set; }
    public long BlackTimeRemainingMs { get; set; }
    public long IncrementMs { get; set; }
    public long LastMoveTimestamp { get; set; }
}
