using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialApp.DTOs;
using SocialApp.Models;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly SocialMediaDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(SocialMediaDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Lấy thông tin người dùng đang đăng nhập
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponseDTO>> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
            return NotFound();
        }

        var userResponse = new UserResponseDTO
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Bio,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LastActive = user.LastActive
        };

        return Ok(userResponse);
    }

    // API để lấy thông tin người dùng theo id
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDTO>> GetUserById(int id)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
            return NotFound();
        }

        var userResponse = new UserResponseDTO
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Bio = user.Bio,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt,
            LastActive = user.LastActive
        };

        return Ok(userResponse);
    }
}
