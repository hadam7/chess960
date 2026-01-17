using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chess960.Web.Data;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Blocked
}

public class Friendship
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string RequesterId { get; set; } = string.Empty;

    [ForeignKey(nameof(RequesterId))]
    public ApplicationUser Requester { get; set; } = default!;

    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser Receiver { get; set; } = default!;

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
