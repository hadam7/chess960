namespace Chess960.Web.Client.Models;

public class GameStartedDto
{
    public string GameId { get; set; } = "";
    public string Fen { get; set; } = "";
    public string WhiteId { get; set; } = "";
    public string BlackId { get; set; } = "";
    public long WhiteTimeMs { get; set; }
    public long BlackTimeMs { get; set; }
    public int WhiteRating { get; set; }
    public int BlackRating { get; set; }
    public string? WhiteAvatar { get; set; }
    public string? BlackAvatar { get; set; }
}
