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
    private readonly IEmailVerificationService _emailVerificationService;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IEmailVerificationService emailVerificationService)
    {
        _authService = authService;
        _logger = logger;
        _emailVerificationService = emailVerificationService;
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
      [HttpPost("verifyemail")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailDTO verifyEmailDto)
    {
        try
        {
            _logger.LogInformation("Email verification requested for: {Email}", verifyEmailDto.Email);
            
            // Check if email already exists in our database
            if (await _authService.EmailExistsAsync(verifyEmailDto.Email))
            {
                _logger.LogInformation("Email {Email} already exists in database", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Email already in use" });
            }

            // Verify email format and existence using the external service
            var verificationResult = await _emailVerificationService.VerifyEmailAsync(verifyEmailDto.Email);
            _logger.LogInformation("Email {Email} verification result: IsValid={IsValid}, Exists={Exists}, Message={Message}", 
                verifyEmailDto.Email, verificationResult.IsValid, verificationResult.Exists, verificationResult.Message);
            
            // Kiểm tra định dạng email
            if (!verificationResult.IsValid)
            {
                _logger.LogInformation("Email {Email} is invalid", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Invalid email format" });
            }
            
            // Kiểm tra sự tồn tại của email
            if (!verificationResult.Exists)
            {
                _logger.LogInformation("Email {Email} doesn't exist", verifyEmailDto.Email);
                return BadRequest(new { 
                    isValid = false, 
                    message = "This email address doesn't appear to exist. Please use a valid, existing email address." 
                });
            }

            return Ok(new { isValid = true, message = "Email is valid" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            return BadRequest(new { isValid = false, message = "Error verifying email. Please try again later." });
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
