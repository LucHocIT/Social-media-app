using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services.Auth;
using SocialApp.Services.Email;

namespace SocialApp.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserAccountService _userAccountService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserAccountService userAccountService,
        IEmailVerificationService emailVerificationService,
        ILogger<AuthController> logger)
    {
        _userAccountService = userAccountService;
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    // Legacy registration endpoint removed

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDTO>> Login(LoginUserDTO loginDto)
    {
        var loginResult = await _userAccountService.LoginAsync(loginDto);

        if (!loginResult.Success)
        {
            // Đăng nhập thất bại - trả về 401 Unauthorized với thông báo lỗi
            return Unauthorized(new { message = loginResult.ErrorMessage });
        }
        // Đăng nhập thành công
        return Ok(loginResult.Result);
    }

    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        _logger.LogInformation("User {Username} (ID: {UserId}) logged out", username, userId);

        return Ok(new { message = "Đăng xuất thành công" });
    }    // Admin functions have been moved to UserManagementController
    [HttpGet("user-info")]
    [Authorize]
    public async Task<ActionResult<UserResponseDTO>> GetUserInfo()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _userAccountService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        return Ok(user);
    }

    // Register/verified endpoint removed
    [HttpPost("sendVerificationCode")]
    public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeDTO sendVerificationCodeDto)
    {
        try
        {
            _logger.LogInformation("Sending verification code to: {Email}", sendVerificationCodeDto.Email);

            var (success, message) = await _emailVerificationService.SendVerificationCodeAsync(sendVerificationCodeDto.Email);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification code to {Email}", sendVerificationCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("verifyCode")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDTO verifyCodeDto)
    {
        try
        {
            _logger.LogInformation("Verifying code for: {Email}", verifyCodeDto.Email);

            var (success, message) = await _emailVerificationService.VerifyCodeAsync(verifyCodeDto.Email, verifyCodeDto.Code);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new
            {
                success = true,
                message,
                readyForRegistration = true,
                email = verifyCodeDto.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying code for {Email}", verifyCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("verifyAndRegister")]
    public async Task<ActionResult<AuthResponseDTO>> VerifyAndRegister([FromBody] VerifiedRegisterDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering verified user: {Email}", registerDto.Email);

            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }

            var result = await _userAccountService.RegisterVerifiedUserAsync(registerDto);
            _logger.LogInformation("User registered successfully after verification: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during verification and registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }// Register endpoint removed
    [HttpPost("forgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO forgotPasswordDto)
    {
        try
        {
            _logger.LogInformation("Password reset requested for: {Email}", forgotPasswordDto.Email);

            var (success, message) = await _emailVerificationService.SendPasswordResetCodeAsync(forgotPasswordDto.Email);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", forgotPasswordDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    [HttpPost("verifyResetCode")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDTO verifyResetCodeDto)
    {
        try
        {
            _logger.LogInformation("Verifying reset code for: {Email}", verifyResetCodeDto.Email);

            var (success, message) = await _emailVerificationService.VerifyPasswordResetCodeAsync(verifyResetCodeDto.Email, verifyResetCodeDto.Code);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reset code for {Email}", verifyResetCodeDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    [HttpPost("resetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO resetPasswordDto)
    {
        try
        {
            _logger.LogInformation("Resetting password for: {Email}", resetPasswordDto.Email);

            var (success, message) = await _emailVerificationService.ResetPasswordAsync(resetPasswordDto);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", resetPasswordDto?.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("social-login")]
    public async Task<ActionResult<AuthResponseDTO>> SocialLogin(SocialLoginDTO socialLoginDto)
    {
        try
        {
            _logger.LogInformation("Social login attempt with provider: {Provider}", socialLoginDto.Provider);

            var loginResult = await _userAccountService.SocialLoginAsync(socialLoginDto);

            if (!loginResult.Success)
            {
                _logger.LogWarning("Social login failed: {ErrorMessage}", loginResult.ErrorMessage);
                return BadRequest(new { message = loginResult.ErrorMessage });
            }

            _logger.LogInformation("Social login successful with provider: {Provider}", socialLoginDto.Provider);
            return Ok(loginResult.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during social login with provider: {Provider}", socialLoginDto.Provider);
            return BadRequest(new { message = "Đăng nhập thất bại: " + ex.Message });
        }
    }
}
