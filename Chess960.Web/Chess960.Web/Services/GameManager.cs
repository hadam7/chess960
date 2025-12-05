using System.Collections.Concurrent;
using Rudzoft.ChessLib;
using Rudzoft.ChessLib.Factories;
using Rudzoft.ChessLib.Types;

namespace Chess960.Web.Services;

public class GameManager
{
    private readonly ConcurrentDictionary<string, GameSession> _games = new();
    private readonly ConcurrentQueue<string> _waitingPlayers = new();

    public GameSession? FindMatch(string playerConnectionId)
    {
        if (_waitingPlayers.TryDequeue(out var opponentId))
        {
            // Prevent matching with self if clicked twice quickly
            if (opponentId == playerConnectionId)
            {
                _waitingPlayers.Enqueue(playerConnectionId);
                return null;
            }
            
            return CreateGame(opponentId, playerConnectionId);
        }
        
        _waitingPlayers.Enqueue(playerConnectionId);
        return null;
    }

    public GameSession CreateGame(string whitePlayerId, string blackPlayerId)
    {
        var gameId = GenerateGameId();
        var game = GameFactory.Create();
        game.NewGame();

        var session = new GameSession
        {
            GameId = gameId,
            HostConnectionId = whitePlayerId, // Host is White for now
            Game = game,
            WhitePlayerId = whitePlayerId,
            BlackPlayerId = blackPlayerId
        };

        _games.TryAdd(gameId, session);
        return session;
    }

    // Kept for manual creation if needed later, but refactored to use common logic could be better
    public GameSession CreateHostedGame(string hostConnectionId, string? fen = null)
    {
        var gameId = GenerateGameId();
        var game = GameFactory.Create(fen ?? GameFactory.Create().Pos.FenNotation);
        
        if (fen == null) game.NewGame();

        var session = new GameSession
        {
            GameId = gameId,
            HostConnectionId = hostConnectionId,
            Game = game,
            WhitePlayerId = hostConnectionId
        };

        _games.TryAdd(gameId, session);
        return session;
    }

    public GameSession? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var session);
        return session;
    }

    public bool JoinGame(string gameId, string playerConnectionId)
    {
        if (_games.TryGetValue(gameId, out var session))
        {
            if (string.IsNullOrEmpty(session.BlackPlayerId))
            {
                session.BlackPlayerId = playerConnectionId;
                return true;
            }
        }
        return false;
    }

    public bool MakeMove(string gameId, string moveString)
    {
        if (_games.TryGetValue(gameId, out var session))
        {
            // Parse and apply move using Rudzoft (simplified for now)
            // In a real app, we'd need robust move validation here matching the client side
            // For this MVP, we'll trust the client sends valid moves or handle exceptions
            try 
            {
                var from = new Square(moveString.Substring(0, 2));
                var to = new Square(moveString.Substring(2, 2));
                
                var moveList = session.Game.Pos.GenerateMoves();
                var move = moveList.FirstOrDefault(m => m.Move.FromSquare() == from && m.Move.ToSquare() == to);
                
                if (!move.Move.Equals(default(Move)))
                {
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
    public string? BlackPlayerId { get; set; }
    public IGame Game { get; set; } = default!;
}
