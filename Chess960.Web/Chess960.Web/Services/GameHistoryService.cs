using Chess960.Web.Data;
using Microsoft.EntityFrameworkCore;
using Chess960.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace Chess960.Web.Services;

public class GameHistoryService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GameHistoryService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task SaveGameAsync(GameSession session, GameResult result, string endReason)
    {
        Console.WriteLine($"[GameHistory] Saving game {session.GameId}...");
        Console.WriteLine($"[GameHistory] WhiteID: {session.WhiteUserId}, BlackID: {session.BlackUserId}");

        var whiteUser = await _userManager.FindByIdAsync(session.WhiteUserId);
        var blackUser = session.BlackUserId != null ? await _userManager.FindByIdAsync(session.BlackUserId) : null;
        
        Console.WriteLine($"[GameHistory] WhiteName: {whiteUser?.UserName ?? "NULL"}, BlackName: {blackUser?.UserName ?? "NULL"}");

        var history = new GameHistory
        {
            WhiteUserId = session.WhiteUserId,
            BlackUserId = session.BlackUserId ?? "Bot",
            WhiteUserName = whiteUser?.UserName ?? "Unknown",
            BlackUserName = blackUser?.UserName ?? "Bot",
            Result = result.ToString(),
            EndReason = endReason.ToString(),
            MovesPgn = string.Join(" ", session.Moves), 
            Fen = session.Game.Pos.FenNotation,
            InitialFen = session.InitialFen,
            TimeControl = session.TimeControl
        };
        

        
        _context.GameHistories.Add(history);
        int rows = await _context.SaveChangesAsync();
        Console.WriteLine($"[GameHistory] Saved Successfully. Rows affected: {rows}");
    }

    public async Task<List<GameHistory>> GetGamesForUserAsync(string userId, int limit = 10)
    {
        Console.WriteLine($"[GameHistory] Fetching games for UserID: {userId}. Limit: {limit}");
        var games = await _context.GameHistories
            .Where(g => g.WhiteUserId == userId || g.BlackUserId == userId)
            .OrderByDescending(g => g.DatePlayed)
            .Take(limit)
            .ToListAsync();
            
        Console.WriteLine($"[GameHistory] Found {games.Count} games.");
        return games;
    }
    public async Task<GameHistory?> GetGameByIdAsync(Guid gameId)
    {
        return await _context.GameHistories.FirstOrDefaultAsync(g => g.Id == gameId);
    }

    public async Task<int> GetGamesPlayedTodayAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.GameHistories.CountAsync(g => g.DatePlayed >= today);
    }
}
