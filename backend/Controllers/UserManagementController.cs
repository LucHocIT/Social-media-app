using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly IUserAccountService _userAccountService;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        IUserManagementService userManagementService,
        IUserAccountService userAccountService,
        ILogger<UserManagementController> logger)
    {
        _userManagementService = userManagementService;
        _userAccountService = userAccountService;
        _logger = logger;
    }

    [HttpPut("users/{userId}/role")]
    public async Task<ActionResult> SetUserRole(int userId, [FromBody] SetUserRoleDTO roleDto)
    {
        _logger.LogInformation("Admin attempting to change role for User ID: {UserId} to {Role}", userId, roleDto.Role);
        
        // Don't allow changing own role
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId.ToString())
        {
            _logger.LogWarning("Admin attempted to change their own role");
            return BadRequest(new { message = "Không thể thay đổi vai trò của chính mình" });
        }
        
        var result = await _userManagementService.SetUserRoleAsync(userId, roleDto.Role);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng hoặc vai trò không hợp lệ" });
        }
        
        return Ok(new { message = $"Đã thiết lập vai trò {roleDto.Role} cho người dùng" });
    }

    [HttpDelete("users/{userId}")]
    public async Task<ActionResult> DeleteUser(int userId)
    {
        _logger.LogInformation("Admin attempting to soft delete User ID: {UserId}", userId);
        
        // Don't allow deleting self
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId.ToString())
        {
            _logger.LogWarning("Admin attempted to delete themselves");
            return BadRequest(new { message = "Không thể xóa tài khoản của chính mình" });
        }
        
        var result = await _userManagementService.SoftDeleteUserAsync(userId);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(new { message = "Người dùng đã bị xóa tạm thời" });
    }

    [HttpPost("users/{userId}/restore")]
    public async Task<ActionResult> RestoreUser(int userId)
    {
        _logger.LogInformation("Admin attempting to restore User ID: {UserId}", userId);
        
        var result = await _userManagementService.RestoreUserAsync(userId);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(new { message = "Người dùng đã được khôi phục" });
    }

    [HttpGet("users/{userId}")]
    public async Task<ActionResult<UserResponseDTO>> GetUser(int userId)
    {
        _logger.LogInformation("Admin requesting details for User ID: {UserId}", userId);
        
        var user = await _userAccountService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(user);
    }
}
