using System.Text;

namespace Chess960.Web.Client.Services;

public static class Chess960Generator
{
    private static readonly Random _random = new Random();

    public static string GenerateStartingFen()
    {
        // 1. Place Bishops on opposite colors
        // 0-3 for light squares (1, 3, 5, 7) -> indices 1, 3, 5, 7
        // 0-3 for dark squares (0, 2, 4, 6) -> indices 0, 2, 4, 6
        
        char[] board = new char[8];
        for (int i = 0; i < 8; i++) board[i] = ' ';

        int bishop1Pos = _random.Next(0, 4) * 2 + 1; // 1, 3, 5, 7 (Light)
        int bishop2Pos = _random.Next(0, 4) * 2;     // 0, 2, 4, 6 (Dark)

        board[bishop1Pos] = 'B';
        board[bishop2Pos] = 'B';

        // 2. Place Queen
        PlaceRandomPiece(board, 'Q');

        // 3. Place Knights
        PlaceRandomPiece(board, 'N');
        PlaceRandomPiece(board, 'N');

        // 4. Place Rooks and King
        // The King must be between the two Rooks.
        // We have 3 empty spots left. The order must be R, K, R.
        
        int emptyCount = 0;
        for (int i = 0; i < 8; i++)
        {
            if (board[i] == ' ')
            {
                if (emptyCount == 0) board[i] = 'R';
                else if (emptyCount == 1) board[i] = 'K';
                else if (emptyCount == 2) board[i] = 'R';
                emptyCount++;
            }
        }

        // Convert to FEN string
        // Lowercase for black (rank 8), Uppercase for white (rank 1)
        
        string whitePieces = new string(board);
        string blackPieces = whitePieces.ToLower();

        // Calculate Castling Rights (Shredder-FEN)
        // Use file letters of the rooks (e.g. HAha)
        var rookIndices = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            if (board[i] == 'R') rookIndices.Add(i);
        }

        string castlingRights = "";
        if (rookIndices.Count == 2)
        {
            // Rooks are at rookIndices[0] (left/queenside) and rookIndices[1] (right/kingside)
            // Convention: usually Capital letters for White, Small for Black.
            // We can just list the file letters.
            char rook1File = (char)('A' + rookIndices[0]);
            char rook2File = (char)('A' + rookIndices[1]);
            
            // Standard FEN order is usually K then Q (Kingside then Queenside).
            // But in Shredder-FEN, we just list the files.
            // Let's list them in file order or specific order? 
            // Usually it simply lists the available castling files.
            // Let's put them all: White Rooks, then Black Rooks.
            
            castlingRights += $"{rook2File}{rook1File}{char.ToLower(rook2File)}{char.ToLower(rook1File)}";
        }
        else
        {
            castlingRights = "-";
        }

        return $"{blackPieces}/pppppppp/8/8/8/8/PPPPPPPP/{whitePieces} w {castlingRights} - 0 1";
    }

    private static void PlaceRandomPiece(char[] board, char piece)
    {
        var emptyIndices = board.Select((c, i) => new { c, i })
                                .Where(x => x.c == ' ')
                                .Select(x => x.i)
                                .ToList();
        
        int randomIndex = emptyIndices[_random.Next(emptyIndices.Count)];
        board[randomIndex] = piece;
    }
}
