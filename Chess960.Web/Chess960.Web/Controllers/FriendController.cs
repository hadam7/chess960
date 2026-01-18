using Microsoft.AspNetCore.Mvc;
using Chess960.Web.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Chess960.Web.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendController : ControllerBase
{
    private readonly FriendService _friendService;

    public FriendController(FriendService friendService)
    {
        _friendService = friendService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var friends = await _friendService.GetFriendsAsync(userId);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var requests = await _friendService.GetPendingRequestsAsync(userId);
        return Ok(requests);
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] string targetUsername)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var error = await _friendService.SendFriendRequestAsync(userId, targetUsername);
        if (error != null)
        {
             return BadRequest(error);
        }
        return Ok();
    }

    [HttpPost("accept/{id}")]
    public async Task<IActionResult> AcceptRequest(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        await _friendService.AcceptFriendRequestAsync(id, userId);
        return Ok();
    }

    [HttpPost("decline/{id}")]
    public async Task<IActionResult> DeclineRequest(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        await _friendService.DeclineFriendRequestAsync(id, userId);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFriend(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        await _friendService.RemoveFriendAsync(id, userId);
        return Ok();
    }
}
