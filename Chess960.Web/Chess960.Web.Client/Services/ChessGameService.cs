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

    public bool? IsWhiteWinner { get; private set; } // null = running/draw, true = White, false = Black

    public void ResignGame(string reason = "Resigned")
    {
        GameOverMessage = reason;
        // Assuming the player (User) is always White vs Bot, or we need to pass who resigned.
        // For now, in PlayVsBot, User (White usually) resigns.
        // If we want generic support, we should pass 'bool isWhiteResigning'.
        // Let's assume IsWhiteSide parameter in PlayVsBot determines this.
        // But here we don't know who is who.
        // Let's overload or assume caller handles logic, but wait, this method sets state.
        // I will change signature to ResignGame(bool isWhiteResigning, string reason) to be precise.
        // But to avoid breaking callers, I'll default to "User (White?) resigned" ??
        // Actually, PlayVsBot calls this. Let's simplisticly assume if this is called, the HUMAN resigned.
        // If Human is White, White Resigned -> Black Wins.
        IsWhiteWinner = false; // Default: User (White) resigned, Bot (Black) wins.
        NotifyStateChanged();
    }
    
    // Better overload
    public void ResignGame(bool isWhiteResigning, string reason = "Resigned")
    {
        GameOverMessage = reason;
        IsWhiteWinner = !isWhiteResigning;
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
        if (Game == null) return;

        if (IsMate)
        {
            // If SideToMove is White, White is mated -> Black wins.
            IsWhiteWinner = !Game.Pos.SideToMove.IsWhite;
            var winner = IsWhiteWinner.Value ? "White" : "Black";
            GameOverMessage = $"Checkmate! {winner} wins.";
        }
        else if (IsStalemate)
        {
             IsWhiteWinner = null; // Draw
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

    public string GetSanForMove(string fen, string lanMove)
    {
        try 
        {
            // Create temp game to validate and generate SAN
            var tempGame = GameFactory.Create(fen);
            var fromSquare = new Square(lanMove.Substring(0, 2));
            var toSquare = new Square(lanMove.Substring(2, 2));
            
            // Generate valid moves from this position
            var moves = tempGame.Pos.GenerateMoves();
            var move = moves.FirstOrDefault(m => m.Move.FromSquare() == fromSquare && m.Move.ToSquare() == toSquare);
            
            if (lanMove.Length > 4) // Promotion
            {
                 // Handle promotion selection if needed, or assume Queen if not specified? 
                 // LAN usually includes promotion char e.g. a7a8q
                 // We should match strict if provided
                 char promoChar = lanMove[4];
                 PieceTypes promoType = GetPromoType(promoChar);
                 move = moves.FirstOrDefault(m => m.Move.FromSquare() == fromSquare && m.Move.ToSquare() == toSquare && m.Move.PromotedPieceType() == promoType);
            }

            if (!move.Move.Equals(default(Move)))
            {
                // Generate SAN
                return GenerateSan(tempGame, move.Move);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating SAN for {lanMove}: {ex.Message}");
        }
        return lanMove; // Fallback to LAN
    }

    private PieceTypes GetPromoType(char c)
    {
        return char.ToLower(c) switch 
        {
            'q' => PieceTypes.Queen,
            'r' => PieceTypes.Rook,
            'b' => PieceTypes.Bishop,
            'n' => PieceTypes.Knight,
            _ => PieceTypes.Queen
        };
    }

    private string GenerateSan(IGame game, Move move)
    {
        // Manual SAN generation (simplified for now, Rudzoft might lack full SAN generator in this version)
        var piece = game.Pos.GetPiece(move.FromSquare());
        var pieceType = piece.Type();
        var isCapture = game.Pos.GetPiece(move.ToSquare()) != Piece.EmptyPiece;
        
        // Check for En Passant capture
        if (pieceType == PieceTypes.Pawn && move.IsEnPassantMove()) isCapture = true;

        // Castling
        if (pieceType == PieceTypes.King && Math.Abs(move.ToSquare().File.Value - move.FromSquare().File.Value) > 1)
        {
             return move.ToSquare().File.Value > move.FromSquare().File.Value ? "O-O" : "O-O-O";
        }

        string san = "";
        
        if (pieceType != PieceTypes.Pawn)
        {
            san += char.ToUpper(GetPieceChar(pieceType));
        }

        // Ambiguity resolution (simplified - strictly correct needed for real chess but ok for now)
        // If multiple pieces of same type can move to same square...
        // We will skip full ambiguity check for MVP unless user complains or we want perfection.
        // Let's add simple File disambiguation if multiple pieces of same type can reach target.
        var ambiguous = game.Pos.GenerateMoves()
            .Where(m => m.Move.ToSquare() == move.ToSquare() && m.Move.FromSquare() != move.FromSquare() && game.Pos.GetPiece(m.Move.FromSquare()).Type() == pieceType)
            .Any();
            
        if (ambiguous && pieceType != PieceTypes.Pawn)
        {
            // Disambiguate by File
            if (game.Pos.GenerateMoves().Count(m => m.Move.ToSquare() == move.ToSquare() && game.Pos.GetPiece(m.Move.FromSquare()).Type() == pieceType && m.Move.FromSquare().File == move.FromSquare().File) > 0)
            {
                 // Files same, use Rank
                 san += move.FromSquare().Rank.Char;
            }
            else
            {
                 san += move.FromSquare().File.Char;
            }
        }
        else if (isCapture && pieceType == PieceTypes.Pawn)
        {
            san += move.FromSquare().File.Char;
        }

        if (isCapture) san += "x";
        
        san += move.ToSquare().ToString();

        if (move.IsPromotionMove())
        {
            san += "=" + char.ToUpper(GetPieceChar(move.PromotedPieceType()));
        }

        // Make move to check for Check/Mate
        game.Pos.MakeMove(move, game.Pos.State);
        if (game.Pos.IsMate) san += "#";
        else if (game.Pos.InCheck) san += "+";

        return san;
    }

    private char GetPieceChar(PieceTypes type)
    {
        return type switch
        {
            PieceTypes.Knight => 'N',
            PieceTypes.Bishop => 'B',
            PieceTypes.Rook => 'R',
            PieceTypes.Queen => 'Q',
            PieceTypes.King => 'K',
            _ => ' '
        };
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
