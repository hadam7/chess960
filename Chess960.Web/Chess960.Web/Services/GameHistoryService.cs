using Chess960.Web.Data;
using Microsoft.EntityFrameworkCore;
using Chess960.Web.Services;

namespace Chess960.Web.Services;

public class GameHistoryService
{
    private readonly ApplicationDbContext _context;

    public GameHistoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SaveGameAsync(GameSession session, GameResult result, string endReason)
    {
        var history = new GameHistory
        {
            WhiteUserId = session.WhiteUserId,
            BlackUserId = session.BlackUserId ?? "Bot",
            WhiteUserName = "", // Would need to fetch or store names in session better. Assuming UI handles display via ID or we fetch here.
            BlackUserName = "",
            Result = result.ToString(),
            EndReason = endReason.ToString(),
            MovesPgn = string.Join(" ", session.Moves), 
            Fen = session.Game.Pos.FenNotation
        };
        
        // Fix: We need Valid Move History.
        // Game.Moves? Game.Board?
        // I'll check Game session object in GameHub more closely or assume simple FEN for now if getting Move List is hard, 
        // BUT user asked for "steps".
        // session.Game in GameHub is likely `IGame`.
        
        // For now, let's write the service with placeholders and fix the Move List extraction in the next step by viewing GameHub/GameManager.
        
        _context.GameHistories.Add(history);
        await _context.SaveChangesAsync();
    }

    public async Task<List<GameHistory>> GetGamesForUserAsync(string userId, int limit = 10)
    {
        return await _context.GameHistories
            .Where(g => g.WhiteUserId == userId || g.BlackUserId == userId)
            .OrderByDescending(g => g.DatePlayed)
            .Take(limit)
            .ToListAsync();
    }
}
