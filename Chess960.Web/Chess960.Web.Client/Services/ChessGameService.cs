using Rudzoft.ChessLib;
using Rudzoft.ChessLib.Factories;
using Rudzoft.ChessLib.Fen;
using Rudzoft.ChessLib.MoveGeneration;
using Rudzoft.ChessLib.Types;

namespace Chess960.Web.Client.Services;

public class ChessGameService
{
    public IGame? Game { get; private set; }
    public bool IsInitialized => Game != null;

    public event Action? OnStateChanged;

    public ChessGameService()
    {
        // Constructor is now lightweight
    }

    public async Task InitializeAsync()
    {
        if (Game != null) return;

        // Offload heavy initialization (Magic Bitboards etc) to background thread
        await Task.Run(() =>
        {
            Game = GameFactory.Create();
            Game.NewGame();
        });
        NotifyStateChanged();
    }

    public void StartNewGame(bool is960 = false)
    {
        if (Game == null) return;

        if (is960)
        {
            string fen = Chess960Generator.GenerateStartingFen();
            Game = GameFactory.Create(fen);
        }
        else
        {
            Game = GameFactory.Create();
            Game.NewGame();
        }
        NotifyStateChanged();
    }
    
    public void StartGameFromFen(string fen)
    {
        // We can create a new game instance synchronously here if the static init is done, 
        // but 'GameFactory.Create(fen)' is fast enough once static statics are loaded.
        Game = GameFactory.Create(fen);
        GameOverMessage = "";
        NotifyStateChanged();
    }

    public void Reset()
    {
        Game = GameFactory.Create();
        Game.NewGame();
        GameOverMessage = "";
        NotifyStateChanged();
    }

    public bool IsCheck => Game?.Pos.InCheck ?? false;
    public bool IsMate => Game?.Pos.IsMate ?? false;
    public bool IsStalemate => !IsCheck && !(Game?.Pos.GenerateMoves().Any() ?? false);
    public string GameOverMessage { get; private set; } = "";

    public bool MakeMove(string fromSquare, string toSquare)
    {
        if (Game == null) return false;

        // Convert string squares to Square types
        var from = new Square(fromSquare);
        var to = new Square(toSquare);

        // Generate all legal moves for the current position
        var moveList = Game.Pos.GenerateMoves();

        // Find a move that matches our from/to squares
        // ExtMove contains the Move object which has the From/To properties
        var move = moveList.FirstOrDefault(m => m.Move.FromSquare() == from && m.Move.ToSquare() == to);

        // If no simple match, check for promotion (Rudzoft might generate separate moves for each promotion type)
        if (move.Move.Equals(default(Move)))
        {
             move = moveList.FirstOrDefault(m => m.Move.FromSquare() == from && m.Move.ToSquare() == to && m.Move.PromotedPieceType() == PieceTypes.Queen);
        }

        if (!move.Move.Equals(default(Move)))
        {
            Game.Pos.MakeMove(move.Move, Game.Pos.State);
            UpdateGameState();
            NotifyStateChanged();
            return true;
        }

        return false;
    }

    public IEnumerable<string> GetLegalMovesFor(string fromSquare)
    {
        if (Game == null) return Enumerable.Empty<string>();

        var from = new Square(fromSquare);
        var moveList = Game.Pos.GenerateMoves();
        
        return moveList
            .Where(m => m.Move.FromSquare() == from)
            .Select(m => m.Move.ToSquare().ToString().ToLower());
    }

    private void UpdateGameState()
    {
        GameOverMessage = "";
        if (IsMate)
        {
            var winner = Game.Pos.SideToMove.IsWhite ? "Black" : "White";
            GameOverMessage = $"Checkmate! {winner} wins.";
        }
        else if (IsStalemate)
        {
            GameOverMessage = "Stalemate! Draw.";
        }
        else if (IsCheck)
        {
             // Just check, game continues
        }
    }

    public string GetFen()
    {
        return Game?.Pos.FenNotation ?? "";
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
