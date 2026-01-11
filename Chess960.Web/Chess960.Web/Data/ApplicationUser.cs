using Microsoft.AspNetCore.Identity;

namespace Chess960.Web.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    public int EloRating { get; set; } = 1200; // Legacy/Overall
    public int EloBullet { get; set; } = 1200;
    public int EloBlitz { get; set; } = 1200;
    public int EloRapid { get; set; } = 1200;
    
    public int GamesPlayed { get; set; } = 0;
    public int GamesWon { get; set; } = 0;
}

