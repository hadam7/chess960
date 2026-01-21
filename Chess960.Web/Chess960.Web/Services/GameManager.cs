using System.Collections.Concurrent;
using Rudzoft.ChessLib;
using Rudzoft.ChessLib.Factories;
using Rudzoft.ChessLib.Types;
using Rudzoft.ChessLib.MoveGeneration;

namespace Chess960.Web.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, GameSession> _games = new();
    // Key: TimeControl (e.g., "3+2"), Value: List of waiting players (Locked for access)
    private readonly ConcurrentDictionary<string, List<MatchTicket>> _waitingLists = new();

    public GameSession? FindMatch(string playerConnectionId, string userId, string timeControl, int userRating, int ratingRange)
    {
        var list = _waitingLists.GetOrAdd(timeControl, _ => new List<MatchTicket>());

        lock (list) 
        {
            // 1. Clean up offline/stale users first? Or check on match? Check on match for efficiency.

            // 2. Iterate to find a match
            for (int i = 0; i < list.Count; i++)
            {
                var opponent = list[i];

                // Skip self
                if (opponent.UserId == userId) continue;

                // Check Connection Validity
                var currentOppConn = GetConnectionId(opponent.UserId);
                if (string.IsNullOrEmpty(currentOppConn))
                {
                    // Remove stale entry
                    list.RemoveAt(i);
                    i--; // Adjust index
                    continue;
                }

                // CHECK RATINGS (Mutual)
                // My requirement: Abs(OpRating - MyRating) <= MyRange
                // Op requirement: Abs(MyRating - OpRating) <= OpRange
                int diff = Math.Abs(userRating - opponent.Rating);
                
                if (diff <= ratingRange && diff <= opponent.RatingRange)
                {
                    // Match Found!
                    Console.WriteLine($"[GameManager] Elo Match: {userId}({userRating}) vs {opponent.UserId}({opponent.Rating}). Diff: {diff}");
                    
                    list.RemoveAt(i); // Remove opponent from queue

                    // Create Game
                    return CreateGame(currentOppConn, opponent.UserId, playerConnectionId, userId, timeControl);
                }
            }

            // 3. No match found -> Add self to list
            // Remove any existing entry for self first (to update rating/range)
            list.RemoveAll(x => x.UserId == userId);
            
            list.Add(new MatchTicket(playerConnectionId, userId, userRating, ratingRange));
            Console.WriteLine($"[GameManager] Enqueued {userId} ({userRating} +/- {ratingRange}) for {timeControl}");
            return null;
        }
    }

    private class MatchTicket
    {
        public string ConnectionId { get; }
        public string UserId { get; }
        public int Rating { get; }
        public int RatingRange { get; }

        public MatchTicket(string connectionId, string userId, int rating, int ratingRange)
        {
            ConnectionId = connectionId;
            UserId = userId;
            Rating = rating;
            RatingRange = ratingRange;
        }
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
            LastMoveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            InitialFen = game.Pos.FenNotation
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

    public MoveResult MakeMove(string gameId, string moveString)
    {
        if (_games.TryGetValue(gameId, out var session))
        {
            if (session.Result != GameResult.Active) 
                return new MoveResult(false, "", 0, 0, session.Result, session.EndReason, session.WinnerUserId);

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
                    
                    bool isWhiteMove = session.Game.Pos.SideToMove.IsWhite;
                    
                    if (isWhiteMove)
                    {
                        session.WhiteTimeRemainingMs -= elapsed;
                        if (session.WhiteTimeRemainingMs < 0) 
                        {
                            EndGame(session, GameResult.BlackWon, GameEndReason.Timeout, session.BlackUserId);
                            return new MoveResult(false, session.Game.Pos.FenNotation, 0, session.BlackTimeRemainingMs, session.Result, session.EndReason, session.WinnerUserId);
                        }
                        session.WhiteTimeRemainingMs += session.IncrementMs;
                    }
                    else
                    {
                        session.BlackTimeRemainingMs -= elapsed;
                        if (session.BlackTimeRemainingMs < 0)
                        {
                            EndGame(session, GameResult.WhiteWon, GameEndReason.Timeout, session.WhiteUserId);
                            return new MoveResult(false, session.Game.Pos.FenNotation, session.WhiteTimeRemainingMs, 0, session.Result, session.EndReason, session.WinnerUserId);
                        }
                        session.BlackTimeRemainingMs += session.IncrementMs;
                    }
                    
                    session.LastMoveTimestamp = now;

                    session.Game.Pos.MakeMove(move.Move, session.Game.Pos.State);
                    session.Moves.Add(moveString);

                    // Check Game Over Conditions
                    if (session.Game.Pos.IsMate)
                    {
                        var winnerId = isWhiteMove ? session.WhiteUserId : session.BlackUserId; 
                        var result = isWhiteMove ? GameResult.WhiteWon : GameResult.BlackWon;
                        EndGame(session, result, GameEndReason.Checkmate, winnerId);
                    }
                    else 
                    {
                        var nextMoves = session.Game.Pos.GenerateMoves();
                        if (!nextMoves.Any() && !session.Game.Pos.InCheck)
                        {
                             EndGame(session, GameResult.Draw, GameEndReason.Stalemate, null);
                        }
                    }

                    return new MoveResult(true, session.Game.Pos.FenNotation, session.WhiteTimeRemainingMs, session.BlackTimeRemainingMs, session.Result, session.EndReason, session.WinnerUserId);
                }
            }
            catch
            {
                // Fallthrough to return false
            }
        }
        return new MoveResult(false, "", 0, 0, GameResult.Active, GameEndReason.None, null);
    }

    public GameSession? Resign(string gameId, string userId)
    {
        if (_games.TryGetValue(gameId, out var session) && session.Result == GameResult.Active)
        {
            if (session.WhiteUserId == userId)
            {
                EndGame(session, GameResult.BlackWon, GameEndReason.Resignation, session.BlackUserId);
                return session;
            }
            else if (session.BlackUserId == userId)
            {
                EndGame(session, GameResult.WhiteWon, GameEndReason.Resignation, session.WhiteUserId);
                return session;
            }
        }
        return null;
    }

    public GameSession? Abort(string gameId, string userId)
    {
        if (_games.TryGetValue(gameId, out var session) && session.Result == GameResult.Active)
        {
            bool canAbort = false;
            if (session.WhiteUserId == userId)
            {
                // White can abort if they haven't made a move yet (Game Start)
                if (session.Moves.Count == 0) canAbort = true;
            }
            else if (session.BlackUserId == userId)
            {
                // Black can abort if they haven't made a move yet (Moves <= 1)
                if (session.Moves.Count <= 1) canAbort = true;
            }

            if (canAbort)
            {
                EndGame(session, GameResult.Aborted, GameEndReason.Aborted, null);
                return session;
            }
        }
        return null;
    }

    public bool OfferDraw(string gameId, string userId)
    {
        if (_games.TryGetValue(gameId, out var session) && session.Result == GameResult.Active)
        {
            if (session.WhiteUserId == userId || session.BlackUserId == userId)
            {
                session.DrawOfferedByUserId = userId;
                return true;
            }
        }
        return false;
    }

    public GameSession? RespondDraw(string gameId, string userId, bool accept)
    {
        if (_games.TryGetValue(gameId, out var session) && session.Result == GameResult.Active)
        {
            // Can only accept if offer exists and NOT from self
            if (!string.IsNullOrEmpty(session.DrawOfferedByUserId) && session.DrawOfferedByUserId != userId)
            {
                if (accept)
                {
                    EndGame(session, GameResult.Draw, GameEndReason.DrawAgreed, null);
                    return session;
                }
                else
                {
                    session.DrawOfferedByUserId = null; // Decline clears the offer
                    return null; // Null means game continues, not ended
                }
            }
        }
        return null;
    }

    private void EndGame(GameSession session, GameResult result, GameEndReason reason, string? winnerId)
    {
        session.Result = result;
        session.EndReason = reason;
        session.WinnerUserId = winnerId;
        // Clean up or archive game logic here
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

public record MoveResult(bool Success, string Fen, long WhiteTimeMs, long BlackTimeMs, GameResult Result, GameEndReason EndReason, string? WinnerId);

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

    // Game State
    public GameResult Result { get; set; } = GameResult.Active;
    public GameEndReason EndReason { get; set; } = GameEndReason.None;
    public string? WinnerUserId { get; set; }
    public string? DrawOfferedByUserId { get; set; } // UserId of player offering draw
    public List<string> Moves { get; set; } = new();
    public string InitialFen { get; set; } = "";
}

public enum GameResult
{
    Active,
    WhiteWon,
    BlackWon,
    Draw,
    Aborted
}

public enum GameEndReason
{
    None,
    Checkmate,
    Resignation,
    Aborted,
    Timeout,
    Stalemate,
    DrawAgreed,
    DrawDeclared // e.g. 50 move rule, repetition (simplified)
}
