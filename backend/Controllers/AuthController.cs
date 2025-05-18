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
    }[HttpPost("register")]
    public async Task<ActionResult<AuthResponseDTO>> Register(RegisterUserDTO registerDto)
    {
        try
        {
            _logger.LogInformation("Registering new user: {Username}, Email: {Email}", 
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
    }      [HttpPost("verifyemail")]
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
            }

            // Skip external verification in development mode
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            if (isDevelopment)
            {
                _logger.LogInformation("Development environment - skipping external email verification for {Email}", verifyEmailDto.Email);
                return Ok(new { isValid = true, message = "Email is valid (development mode)" });
            }

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
                // Log the error but allow verification to proceed
                _logger.LogWarning(ex, "External email verification service failed for {Email}, proceeding anyway", verifyEmailDto.Email);
                // Continue with basic validation passed
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
