using Rudzoft.ChessLib;
using Rudzoft.ChessLib.Factories;
using Rudzoft.ChessLib.Fen;
using Rudzoft.ChessLib.MoveGeneration;
using Rudzoft.ChessLib.Types;

namespace Chess960.Web.Client.Services;

public class ChessGameService
{
    public IGame Game { get; private set; }

    public event Action? OnStateChanged;

    public ChessGameService()
    {
        // Initialize with a standard game by default, or empty
        Game = GameFactory.Create();
        Game.NewGame();
    }

    public void StartNewGame(bool is960 = false)
    {
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
        Game = GameFactory.Create(fen);
        NotifyStateChanged();
    }

    public bool MakeMove(string fromSquare, string toSquare)
    {
        // Simple move parsing and validation logic
        // This is a simplified example. Rudzoft.ChessLib uses Move types.
        
        // We need to convert string squares (e.g. "e2") to Square types
        // and find the matching legal move.
        
        var moveList = Game.Pos.GenerateMoves();
        
        // TODO: Implement robust move finding based on strings
        // For now, returning false to indicate "not implemented fully"
        
        NotifyStateChanged();
        return false; 
    }

    public string GetFen()
    {
        return Game.Pos.FenNotation;
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
