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
    private readonly IWebHostEnvironment _env;

    public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IWebHostEnvironment env)
    {
        _userManager = userManager;
        _context = context;
        _env = env;
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
            ProfilePictureUrl = user.ProfilePictureUrl,
            LastUsernameChange = user.LastUsernameChange,
            // Stats
            EloRating = user.EloRating,
            EloBullet = user.EloBullet,
            EloBlitz = user.EloBlitz,
            EloRapid = user.EloRapid,
            GamesPlayed = user.GamesPlayed,
            GamesWon = user.GamesWon
        });
    }

    [HttpPost("avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("User not found.");

        if (file == null || file.Length == 0)
            return BadRequest("File is empty.");

        // Validate type
        if (!file.ContentType.StartsWith("image/"))
            return BadRequest("Only images are allowed.");

        if (file.Length > 2 * 1024 * 1024) // 2MB limit
            return BadRequest("Max file size is 2MB.");

        // Ensure directory exists
        var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        // Save file
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{user.Id}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Delete old avatar if exists and not default? 
        // For simplicity, just overwrite URL. User can clean up manually or we can add logic later.

        user.ProfilePictureUrl = $"/avatars/{fileName}";
        await _userManager.UpdateAsync(user);

        return Ok(new { Url = user.ProfilePictureUrl });
    }

    [HttpPost("username")]
    [Authorize]
    public async Task<IActionResult> ChangeUsername([FromBody] ChangeUsernameRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("User not found.");

        if (string.IsNullOrWhiteSpace(request.NewUsername)) return BadRequest("Username cannot be empty.");

        // Check 30 days limit
        if (user.LastUsernameChange.HasValue && user.LastUsernameChange.Value.AddDays(30) > DateTime.UtcNow)
        {
            var daysLeft = (user.LastUsernameChange.Value.AddDays(30) - DateTime.UtcNow).Days;
            return BadRequest($"You can change your username again in {daysLeft} days.");
        }

        var result = await _userManager.SetUserNameAsync(user, request.NewUsername);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.FirstOrDefault()?.Description ?? "Failed to update username.");
        }

        user.LastUsernameChange = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(new { Username = user.UserName });
    }

    public class ChangeUsernameRequest
    {
        public string NewUsername { get; set; } = "";
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
