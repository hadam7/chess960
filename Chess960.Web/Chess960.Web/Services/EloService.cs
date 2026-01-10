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

    public async Task<(int whiteNew, int blackNew, int whiteDelta, int blackDelta)> UpdateRatingsAsync(string whiteName, string blackName, GameResult result)
    {
        var whiteUser = await _userManager.FindByNameAsync(whiteName);
        var blackUser = await _userManager.FindByNameAsync(blackName);

        if (whiteUser == null || blackUser == null) return (0, 0, 0, 0);

        int kFactor = 32;

        double expectedWhite = 1 / (1.0 + Math.Pow(10, (blackUser.EloRating - whiteUser.EloRating) / 400.0));
        double expectedBlack = 1 / (1.0 + Math.Pow(10, (whiteUser.EloRating - blackUser.EloRating) / 400.0));

        double actualWhite = 0.5;
        if (result == GameResult.WhiteWon) actualWhite = 1.0;
        else if (result == GameResult.BlackWon) actualWhite = 0.0;

        double actualBlack = 1.0 - actualWhite;

        int whiteDelta = (int)(kFactor * (actualWhite - expectedWhite));
        int blackDelta = (int)(kFactor * (actualBlack - expectedBlack));

        whiteUser.EloRating += whiteDelta;
        blackUser.EloRating += blackDelta;
        
        whiteUser.GamesPlayed++;
        blackUser.GamesPlayed++;
        
        if (actualWhite == 1.0) whiteUser.GamesWon++;
        if (actualBlack == 1.0) blackUser.GamesWon++;

        await _userManager.UpdateAsync(whiteUser);
        await _userManager.UpdateAsync(blackUser);

        return (whiteUser.EloRating, blackUser.EloRating, whiteDelta, blackDelta);
    }

    public async Task<(int whiteRating, int blackRating)> GetRatingsAsync(string whiteName, string blackName)
    {
        var whiteUser = await _userManager.FindByNameAsync(whiteName);
        var blackUser = await _userManager.FindByNameAsync(blackName);

        return (whiteUser?.EloRating ?? 1200, blackUser?.EloRating ?? 1200);
    }
}
