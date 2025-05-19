using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.DTOs;
using SocialApp.Services;

namespace SocialApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService, 
        ILogger<AuthController> logger, 
        IEmailVerificationService emailVerificationService,
        IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _emailVerificationService = emailVerificationService;
        _configuration = configuration;
    }    [HttpPost("register/legacy")]
    [ApiExplorerSettings(IgnoreApi = true)] // Hide this from Swagger/UI as we're now using the verified registration flow
    public async Task<ActionResult<AuthResponseDTO>> RegisterLegacy(RegisterUserDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering new user (legacy): {Username}, Email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }
            
            // Log received data for debugging
            _logger.LogInformation("Registration data: Username={Username}, Email={Email}, " +
                "FirstName={FirstName}, LastName={LastName}",
                registerDto.Username, registerDto.Email, registerDto.FirstName, registerDto.LastName);
                
            var result = await _authService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDTO>> Login(LoginUserDTO loginDto)
    {
        var loginResult = await _authService.LoginAsync(loginDto);
        
        if (!loginResult.Success)
        {
            // Đăng nhập thất bại - trả về 401 Unauthorized với thông báo lỗi
            return Unauthorized(new { message = loginResult.ErrorMessage });
        }
        
        // Đăng nhập thành công
        return Ok(loginResult.Result);
    }

    [HttpPost("verifyemail")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailDTO verifyEmailDto)
    {
        try
        {
            _logger.LogInformation("Email verification requested for: {Email}", verifyEmailDto.Email);
            
            // Basic email format validation first
            try
            {
                var addr = new System.Net.Mail.MailAddress(verifyEmailDto.Email);
                if (addr.Address != verifyEmailDto.Email)
                {
                    _logger.LogInformation("Email {Email} has invalid format", verifyEmailDto.Email);
                    return BadRequest(new { isValid = false, message = "Invalid email format" });
                }
            }
            catch
            {
                _logger.LogInformation("Email {Email} has invalid format", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Invalid email format" });
            }
            
            // Check if email already exists in our database
            if (await _authService.EmailExistsAsync(verifyEmailDto.Email))
            {
                _logger.LogInformation("Email {Email} already exists in database", verifyEmailDto.Email);
                return BadRequest(new { isValid = false, message = "Email already in use" });
            }            // Comment out this section to enable email verification even in development mode
            // var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            // if (isDevelopment)
            // {
            //     _logger.LogInformation("Development environment - skipping external email verification for {Email}", verifyEmailDto.Email);
            //     return Ok(new { isValid = true, message = "Email is valid (development mode)" });
            // }
            
            try 
            {
                // Verify email format and existence using the external service with timeout handling
                var verificationResult = await _emailVerificationService.VerifyEmailAsync(verifyEmailDto.Email);
                _logger.LogInformation("Email {Email} verification result: IsValid={IsValid}, Exists={Exists}, Message={Message}", 
                    verifyEmailDto.Email, verificationResult.IsValid, verificationResult.Exists, verificationResult.Message);
                
                // Check email format
                if (!verificationResult.IsValid)
                {
                    _logger.LogInformation("Email {Email} is invalid", verifyEmailDto.Email);
                    return BadRequest(new { isValid = false, message = "Invalid email format" });
                }
                
                // Check if email exists
                if (!verificationResult.Exists)
                {
                    _logger.LogInformation("Email {Email} doesn't exist", verifyEmailDto.Email);
                    return BadRequest(new { 
                        isValid = false, 
                        message = "This email address doesn't appear to exist. Please use a valid, existing email address." 
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the error and don't allow verification to proceed with potentially invalid emails
                _logger.LogWarning(ex, "External email verification service failed for {Email}", verifyEmailDto.Email);
                return BadRequest(new { 
                    isValid = false, 
                    message = "Could not verify email. Please try again later or contact support."
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
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        
        _logger.LogInformation("User {Username} (ID: {UserId}) logged out", username, userId);
        
        return Ok(new { message = "Đăng xuất thành công" });
    }

    // Admin endpoints for user management

    [HttpPut("users/{userId}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SetUserRole(int userId, [FromBody] SetUserRoleDTO roleDto)
    {
        _logger.LogInformation("Admin attempting to change role for User ID: {UserId} to {Role}", userId, roleDto.Role);
        
        // Don't allow changing own role
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId.ToString())
        {
            _logger.LogWarning("Admin attempted to change their own role");
            return BadRequest(new { message = "Không thể thay đổi vai trò của chính mình" });
        }
        
        var result = await _authService.SetUserRoleAsync(userId, roleDto.Role);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng hoặc vai trò không hợp lệ" });
        }
        
        return Ok(new { message = $"Đã thiết lập vai trò {roleDto.Role} cho người dùng" });
    }

    [HttpDelete("users/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteUser(int userId)
    {
        _logger.LogInformation("Admin attempting to soft delete User ID: {UserId}", userId);
        
        // Don't allow deleting self
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId.ToString())
        {
            _logger.LogWarning("Admin attempted to delete themselves");
            return BadRequest(new { message = "Không thể xóa tài khoản của chính mình" });
        }
        
        var result = await _authService.SoftDeleteUserAsync(userId);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(new { message = "Người dùng đã bị xóa tạm thời" });
    }

    [HttpPost("users/{userId}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RestoreUser(int userId)
    {
        _logger.LogInformation("Admin attempting to restore User ID: {UserId}", userId);
        
        var result = await _authService.RestoreUserAsync(userId);
        if (!result)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(new { message = "Người dùng đã được khôi phục" });
    }

    [HttpGet("users/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponseDTO>> GetUser(int userId)
    {
        _logger.LogInformation("Admin requesting details for User ID: {UserId}", userId);
        
        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }
        
        return Ok(user);
    }
      [HttpPost("register/verified")]
    public async Task<ActionResult<AuthResponseDTO>> RegisterVerified(RegisterWithVerificationDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering new user with verification: {Username}, Email: {Email}", 
                registerDto.Username, registerDto.Email);
                
            if (registerDto == null)
            {
                _logger.LogWarning("Registration data is null");
                return BadRequest(new { message = "Registration data cannot be empty" });
            }
            
            // Log received data for debugging
            _logger.LogInformation("Registration data: Username={Username}, Email={Email}, " +
                "FirstName={FirstName}, LastName={LastName}",
                registerDto.Username, registerDto.Email, registerDto.FirstName, registerDto.LastName);
                
            var result = await _authService.RegisterAsync(registerDto);
            _logger.LogInformation("User registered successfully: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpPost("sendVerificationCode")]
    public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeDTO sendVerificationCodeDto)
    {
        try
        {
            _logger.LogInformation("Sending verification code to: {Email}", sendVerificationCodeDto.Email);
            
            var (success, message) = await _authService.SendVerificationCodeAsync(sendVerificationCodeDto.Email);
            
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
            
            var (success, message) = await _authService.VerifyCodeAsync(verifyCodeDto.Email, verifyCodeDto.Code);
            
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }
            
            return Ok(new { 
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
            
            var result = await _authService.RegisterVerifiedUserAsync(registerDto);
            _logger.LogInformation("User registered successfully after verification: {Username}", registerDto.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during verification and registration for user {Username}", registerDto?.Username);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register")]
    public ActionResult RedirectToVerifiedRegistration()
    {
        // This is just a redirect to maintain backward compatibility
        return RedirectToAction(nameof(RegisterVerified));
    }
    
    [HttpPost("forgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO forgotPasswordDto)
    {
        try
        {
            _logger.LogInformation("Password reset requested for: {Email}", forgotPasswordDto.Email);
            
            var (success, message) = await _authService.SendPasswordResetCodeAsync(forgotPasswordDto.Email);
            
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
            
            var (success, message) = await _authService.VerifyPasswordResetCodeAsync(verifyResetCodeDto.Email, verifyResetCodeDto.Code);
            
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
            
            var (success, message) = await _authService.ResetPasswordAsync(resetPasswordDto);
            
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
}
