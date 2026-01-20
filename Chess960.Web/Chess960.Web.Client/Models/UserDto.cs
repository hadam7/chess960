namespace Chess960.Web.Client.Models;

public class UserDto
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public int EloRating { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
}
