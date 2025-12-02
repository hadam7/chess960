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
        // Standard Chess960 FEN: 
        // [Black Pieces]/pppppppp/8/8/8/8/PPPPPPPP/[White Pieces] w KQkq - 0 1
        
        string whitePieces = new string(board);
        string blackPieces = whitePieces.ToLower();

        return $"{blackPieces}/pppppppp/8/8/8/8/PPPPPPPP/{whitePieces} w KQkq - 0 1";
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
