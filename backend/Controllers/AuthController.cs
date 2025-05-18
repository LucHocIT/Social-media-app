using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDTO>> Register(RegisterUserDTO registerDto)
    {
        try
        {
            var result = await _authService.RegisterAsync(registerDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDTO>> Login(LoginUserDTO loginDto)
    {
        try
        {
            var result = await _authService.LoginAsync(loginDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout()
    {
        // Với JWT, đăng xuất thực sự được xử lý tại client bằng cách xóa token
        // Server có thể thêm logic như ghi log hoặc thêm token vào blacklist nếu cần
        
        // Lấy thông tin người dùng hiện tại
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        
        _logger.LogInformation("User {Username} (ID: {UserId}) logged out", username, userId);
        
        return Ok(new { message = "Đăng xuất thành công" });
    }
}
