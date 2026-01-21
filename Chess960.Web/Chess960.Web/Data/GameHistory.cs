using System.ComponentModel.DataAnnotations;

namespace Chess960.Web.Data;

public class GameHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string WhiteUserId { get; set; } = string.Empty;
    public string BlackUserId { get; set; } = string.Empty;

    public string WhiteUserName { get; set; } = string.Empty;
    public string BlackUserName { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty; // e.g. "WhiteWon", "Draw"
    public string EndReason { get; set; } = string.Empty; // e.g. "Checkmate", "Resignation"
    public string TimeControl { get; set; } = string.Empty; // e.g. "600+0", "180+2"

    public string MovesPgn { get; set; } = string.Empty; // Store moves
    public string Fen { get; set; } = string.Empty; // Final position
    public string InitialFen { get; set; } = string.Empty; // Starting position (Crucial for Chess960)

    public DateTime DatePlayed { get; set; } = DateTime.UtcNow;
}
