using Chess960.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace Chess960.Web.Services;

public class FriendService
{

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<GameHub> _hubContext;

    public FriendService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<GameHub> hubContext)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
    }

    public async Task<bool> SendFriendRequestAsync(string requesterId, string targetUsername)
    {
        var targetUser = await _userManager.FindByNameAsync(targetUsername);
        if (targetUser == null) return false;
        if (targetUser.Id == requesterId) return false;

        // Check if exists
        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => 
                (f.RequesterId == requesterId && f.ReceiverId == targetUser.Id) ||
                (f.RequesterId == targetUser.Id && f.ReceiverId == requesterId));

        if (existing != null)
        {
            // If was declined/blocked, maybe allow resend? For now, just return false if any link exists
            if (existing.Status == FriendshipStatus.Pending || existing.Status == FriendshipStatus.Accepted)
                return false;
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            ReceiverId = targetUser.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        // Notify Recipient
        // Resolving the name: We can try to look it up.
        var requester = await _userManager.FindByIdAsync(requesterId);
        string requesterName = requester?.UserName ?? "Unknown";

        await _hubContext.Clients.User(targetUser.Id).SendAsync("FriendRequestReceived", requesterId, requesterName);
        
        return true;
    }

    public async Task<List<FriendDto>> GetFriendsAsync(string userId)
    {
        var friendships = await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Receiver)
            .Where(f => (f.RequesterId == userId || f.ReceiverId == userId) && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();

        return friendships.Select(f => 
        {
            var isRequester = f.RequesterId == userId;
            var friend = isRequester ? f.Receiver : f.Requester;
            return new FriendDto
            {
                FriendshipId = f.Id,
                UserId = friend.Id,
                UserName = friend.UserName ?? "Unknown",
                Status = "Online" // We'll handle online status in Hub/UI
            };
        }).ToList();
    }

    public async Task<List<FriendRequestDto>> GetPendingRequestsAsync(string userId)
    {
        // Requests SENT to me
        var requests = await _context.Friendships
            .Include(f => f.Requester)
            .Where(f => f.ReceiverId == userId && f.Status == FriendshipStatus.Pending)
            .ToListAsync();

        return requests.Select(f => new FriendRequestDto
        {
            FriendshipId = f.Id,
            RequesterId = f.RequesterId,
            RequesterName = f.Requester.UserName ?? "Unknown",
            SentAt = f.CreatedAt
        }).ToList();
    }

    public async Task AcceptFriendRequestAsync(int friendshipId, string userId)
    {
        var friendship = await _context.Friendships.FindAsync(friendshipId);
        if (friendship == null) return;
        if (friendship.ReceiverId != userId) return; // Security check

        friendship.Status = FriendshipStatus.Accepted;
        await _context.SaveChangesAsync();
    }

    public async Task DeclineFriendRequestAsync(int friendshipId, string userId)
    {
        var friendship = await _context.Friendships.FindAsync(friendshipId);
        if (friendship == null) return;
        if (friendship.ReceiverId != userId) return;

        _context.Friendships.Remove(friendship); // Or set to Declined
        await _context.SaveChangesAsync();
    }

    public async Task RemoveFriendAsync(int friendshipId, string userId)
    {
        var friendship = await _context.Friendships.FindAsync(friendshipId);
        if (friendship == null) return;
        
        // Ensure the caller is one of the participants
        if (friendship.RequesterId != userId && friendship.ReceiverId != userId) return;

        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
    }
}

public class FriendDto
{
    public int FriendshipId { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Status { get; set; } = "Offline";
}

public class FriendRequestDto
{
    public int FriendshipId { get; set; }
    public string RequesterId { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public DateTime SentAt { get; set; }
}
