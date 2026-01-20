namespace Chess960.Web.Client.Models;

public class GameHistoryDto
{
    public Guid Id { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // "Won", "Lost", "Draw"
    public string EndReason { get; set; } = string.Empty; // "Checkmate", "Time", etc
    public DateTime DatePlayed { get; set; }
}
