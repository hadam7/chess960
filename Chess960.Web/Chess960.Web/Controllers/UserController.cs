using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Chess960.Web.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Chess960.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Return core identity details needed by Client
        return Ok(new 
        { 
            UserId = user.Id,
            UserName = user.UserName,
            // Stats
            EloRating = user.EloRating,
            GamesPlayed = user.GamesPlayed,
            GamesWon = user.GamesWon
        });
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetGameHistory()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var history = await _context.GameHistories
            .Where(g => g.WhiteUserId == userId || g.BlackUserId == userId)
            .OrderByDescending(g => g.DatePlayed)
            .Take(5)
            .Select(g => new
            {
                g.Id,
                g.WhiteUserId,
                g.WhiteUserName,
                g.BlackUserId,
                g.BlackUserName,
                g.Result,
                g.EndReason,
                g.DatePlayed
            })
            .ToListAsync();

        var historyDtos = history.Select(g => new Chess960.Web.Client.Models.GameHistoryDto
        {
            Id = g.Id,
            OpponentName = g.WhiteUserId == userId ? g.BlackUserName : g.WhiteUserName,
            // Determine result relative to the user
            Result = DetermineResult(g.Result, g.WhiteUserId == userId), 
            EndReason = g.EndReason,
            DatePlayed = g.DatePlayed
        }).ToList();

        return Ok(historyDtos);
    }

    private static string DetermineResult(string gameResult, bool isUserWhite)
    {
        // gameResult is usually "WhiteWon", "BlackWon", "Draw"
        if (gameResult == "Draw") return "Draw";
        
        if (isUserWhite)
        {
            return gameResult == "WhiteWon" ? "Won" : "Lost";
        }
        else // User is Black
        {
            return gameResult == "BlackWon" ? "Won" : "Lost";
        }
    }
}
