using Chess960.Web.Data;
using Chess960.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace Chess960.Web.Services;

public class EloService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EloService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(int whiteNew, int blackNew, int whiteDelta, int blackDelta)> UpdateRatingsAsync(string? whiteUserId, string? blackUserId, GameResult result, string timeControl)
    {
        if (string.IsNullOrEmpty(whiteUserId) || string.IsNullOrEmpty(blackUserId)) return (0, 0, 0, 0);

        var whiteUser = await _userManager.FindByIdAsync(whiteUserId);
        var blackUser = await _userManager.FindByIdAsync(blackUserId);

        if (whiteUser == null || blackUser == null) return (0, 0, 0, 0);

        var format = GetFormat(timeControl);
        
        int whiteRating = GetRating(whiteUser, format);
        int blackRating = GetRating(blackUser, format);

        int kFactor = 32;

        double expectedWhite = 1 / (1.0 + Math.Pow(10, (blackRating - whiteRating) / 400.0));
        double expectedBlack = 1 / (1.0 + Math.Pow(10, (whiteRating - blackRating) / 400.0));

        double actualWhite = 0.5;
        if (result == GameResult.WhiteWon) actualWhite = 1.0;
        else if (result == GameResult.BlackWon) actualWhite = 0.0;

        double actualBlack = 1.0 - actualWhite;

        int whiteDelta = (int)(kFactor * (actualWhite - expectedWhite));
        int blackDelta = (int)(kFactor * (actualBlack - expectedBlack));

        // Update Specific Rating
        SetRating(whiteUser, format, whiteRating + whiteDelta);
        SetRating(blackUser, format, blackRating + blackDelta);
        
        // Also update legacy overall rating (optional, or just average?) 
        // For now, let's keep legacy synced with the most played or just last played?
        // User requested 3 visible ratings. Let's just update the specific one.
        // We can update the "Main" EloRating to match the played format to show *something* on leaderboards if they use it.
        whiteUser.EloRating = whiteRating + whiteDelta; 
        blackUser.EloRating = blackRating + blackDelta;

        whiteUser.GamesPlayed++;
        blackUser.GamesPlayed++;
        
        if (actualWhite == 1.0) whiteUser.GamesWon++;
        if (actualBlack == 1.0) blackUser.GamesWon++;

        await _userManager.UpdateAsync(whiteUser);
        await _userManager.UpdateAsync(blackUser);

        return (GetRating(whiteUser, format), GetRating(blackUser, format), whiteDelta, blackDelta);
    }

    public async Task<(int whiteRating, int blackRating)> GetRatingsAsync(string? whiteUserId, string? blackUserId, string timeControl)
    {
        if (string.IsNullOrEmpty(whiteUserId)) return (1200, 1200);

        var whiteUser = await _userManager.FindByIdAsync(whiteUserId);
        ApplicationUser? blackUser = null;
        
        if (!string.IsNullOrEmpty(blackUserId))
        {
            blackUser = await _userManager.FindByIdAsync(blackUserId);
        }

        var format = GetFormat(timeControl);

        return (whiteUser != null ? GetRating(whiteUser, format) : 1200, 
                blackUser != null ? GetRating(blackUser, format) : 1200);
    }

    private string GetFormat(string timeControl)
    {
        // "3+2" -> 3 mins
        if (string.IsNullOrEmpty(timeControl)) return "Blitz";
        try 
        {
            var parts = timeControl.Split('+');
            int mins = int.Parse(parts[0]);
            if (mins < 3) return "Bullet";
            if (mins < 10) return "Blitz";
            return "Rapid";
        }
        catch 
        {
            return "Blitz";
        }
    }

    private int GetRating(ApplicationUser user, string format)
    {
        return format switch
        {
            "Bullet" => user.EloBullet,
            "Blitz" => user.EloBlitz,
            "Rapid" => user.EloRapid,
            _ => user.EloBlitz
        };
    }

    private void SetRating(ApplicationUser user, string format, int newRating)
    {
        switch (format)
        {
            case "Bullet": user.EloBullet = newRating; break;
            case "Blitz": user.EloBlitz = newRating; break;
            case "Rapid": user.EloRapid = newRating; break;
        }
    }
}
